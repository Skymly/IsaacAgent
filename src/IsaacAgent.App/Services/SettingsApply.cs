using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// Settings apply: swap chat provider from provider intent; kick off Embedding apply
/// only when embedding-related fields changed. Explicit RebuildIndexAsync shares the
/// same in-flight cancellation gate so Embedding apply never clears the store while
/// a manual rebuild still holds the retriever build lock.
/// </summary>
public sealed class SettingsApply : ISettingsApply
{
    private readonly ChatServiceProxy _chatProxy;
    private readonly Func<ProviderConfig, IChatService> _buildChat;
    private readonly IEmbeddingApply _embeddingApply;
    private readonly Func<EmbeddingConfig, IEmbeddingProvider> _buildEmbedding;
    private readonly IRetriever _retriever;
    private readonly CancellationToken _shutdownToken;
    private readonly ILogger<SettingsApply>? _logger;
    private readonly object _gate = new();

    private EmbeddingConfig _lastRequestedEmbedding;
    private CancellationTokenSource? _rebuildCts;
    private Task _inFlightTask = Task.CompletedTask;

    public SettingsApply(
        ChatServiceProxy chatProxy,
        Func<ProviderConfig, IChatService> buildChat,
        IEmbeddingApply embeddingApply,
        Func<EmbeddingConfig, IEmbeddingProvider> buildEmbedding,
        EmbeddingConfig initialEmbedding,
        IRetriever retriever,
        CancellationToken shutdownToken = default,
        ILogger<SettingsApply>? logger = null)
    {
        _chatProxy = chatProxy;
        _buildChat = buildChat;
        _embeddingApply = embeddingApply;
        _buildEmbedding = buildEmbedding;
        _lastRequestedEmbedding = initialEmbedding;
        _retriever = retriever;
        _shutdownToken = shutdownToken;
        _logger = logger;
    }

    public void Apply(ProviderIntent intent, ISettingsApplyProgress progress)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(progress);

        _chatProxy.Replace(_buildChat(intent.Chat));

        if (intent.Embedding == _lastRequestedEmbedding)
            return;

        var previousEmbedding = _lastRequestedEmbedding;
        _lastRequestedEmbedding = intent.Embedding;

        CancellationTokenSource linkedCts;
        Task previous;
        lock (_gate)
        {
            _rebuildCts?.Cancel();
            // In-flight task disposes its own CTS in finally — do not Dispose here.
            previous = _inFlightTask;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken);
            _rebuildCts = linkedCts;
            _inFlightTask = Task.Run(
                () => RunEmbeddingRebuildAsync(previous, intent.Embedding, previousEmbedding, linkedCts, progress));
        }

        progress.OnRebuildStarted();
    }

    public Task RebuildIndexAsync(ISettingsApplyProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        CancellationTokenSource linkedCts;
        Task previous;
        Task run;
        lock (_gate)
        {
            _rebuildCts?.Cancel();
            previous = _inFlightTask;
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken);
            _rebuildCts = linkedCts;
            run = Task.Run(() => RunManualRebuildAsync(previous, linkedCts, progress));
            _inFlightTask = run;
        }

        return run;
    }

    private async Task RunEmbeddingRebuildAsync(
        Task previous,
        EmbeddingConfig embedding,
        EmbeddingConfig previousEmbedding,
        CancellationTokenSource linkedCts,
        ISettingsApplyProgress progress)
    {
        try
        {
            await AwaitPreviousAsync(previous).ConfigureAwait(false);

            var provider = _buildEmbedding(embedding);
            await _embeddingApply.ApplyAsync(provider, linkedCts.Token).ConfigureAwait(false);
            if (!linkedCts.IsCancellationRequested)
                progress.OnRebuildSucceeded("Index rebuilt successfully.");
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer apply/rebuild, or shutdown — no failure toast.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Knowledge index rebuild failed");
            lock (_gate)
            {
                // Allow the same provider intent to retry Embedding apply after failure.
                if (ReferenceEquals(_rebuildCts, linkedCts))
                    _lastRequestedEmbedding = previousEmbedding;
            }

            if (!linkedCts.IsCancellationRequested)
                progress.OnRebuildFailed($"Index rebuild failed: {ex.Message}");
        }
        finally
        {
            FinishRebuild(linkedCts, progress);
        }
    }

    private async Task RunManualRebuildAsync(
        Task previous,
        CancellationTokenSource linkedCts,
        ISettingsApplyProgress progress)
    {
        progress.OnRebuildStarted();
        try
        {
            await AwaitPreviousAsync(previous).ConfigureAwait(false);
            linkedCts.Token.ThrowIfCancellationRequested();

            await _retriever.RebuildIndexAsync(linkedCts.Token).ConfigureAwait(false);
            if (!linkedCts.IsCancellationRequested)
                progress.OnRebuildSucceeded("Index rebuilt successfully.");
        }
        catch (OperationCanceledException)
        {
            // Superseded by Embedding apply / newer rebuild, or shutdown — no failure toast.
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Knowledge index rebuild failed");
            if (!linkedCts.IsCancellationRequested)
                progress.OnRebuildFailed($"Index rebuild failed: {ex.Message}");
        }
        finally
        {
            FinishRebuild(linkedCts, progress);
        }
    }

    private void FinishRebuild(CancellationTokenSource linkedCts, ISettingsApplyProgress progress)
    {
        lock (_gate)
        {
            if (ReferenceEquals(_rebuildCts, linkedCts))
            {
                progress.OnRebuildFinished();
                _rebuildCts = null;
            }
        }

        linkedCts.Dispose();
    }

    private static async Task AwaitPreviousAsync(Task previous)
    {
        try
        {
            await previous.ConfigureAwait(false);
        }
        catch
        {
            // Prior rebuild cancelled or failed; continue with the newer request.
        }
    }
}
