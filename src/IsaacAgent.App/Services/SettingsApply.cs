using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// Settings apply: swap chat provider from provider intent; kick off Embedding apply
/// only when embedding-related fields changed. Rebuild runs in the background.
/// </summary>
public sealed class SettingsApply : ISettingsApply
{
    private readonly ChatServiceProxy _chatProxy;
    private readonly Func<ProviderConfig, IChatService> _buildChat;
    private readonly IEmbeddingApply _embeddingApply;
    private readonly Func<EmbeddingConfig, IEmbeddingProvider> _buildEmbedding;
    private readonly CancellationToken _shutdownToken;
    private readonly ILogger<SettingsApply>? _logger;
    private readonly object _gate = new();

    private EmbeddingConfig _lastRequestedEmbedding;
    private CancellationTokenSource? _rebuildCts;

    public SettingsApply(
        ChatServiceProxy chatProxy,
        Func<ProviderConfig, IChatService> buildChat,
        IEmbeddingApply embeddingApply,
        Func<EmbeddingConfig, IEmbeddingProvider> buildEmbedding,
        EmbeddingConfig initialEmbedding,
        CancellationToken shutdownToken = default,
        ILogger<SettingsApply>? logger = null)
    {
        _chatProxy = chatProxy;
        _buildChat = buildChat;
        _embeddingApply = embeddingApply;
        _buildEmbedding = buildEmbedding;
        _lastRequestedEmbedding = initialEmbedding;
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
        lock (_gate)
        {
            _rebuildCts?.Cancel();
            // In-flight task disposes its own CTS in finally — do not Dispose here.
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_shutdownToken);
            _rebuildCts = linkedCts;
        }

        var provider = _buildEmbedding(intent.Embedding);
        progress.OnRebuildStarted();

        _ = Task.Run(async () =>
        {
            try
            {
                await _embeddingApply.ApplyAsync(provider, linkedCts.Token).ConfigureAwait(false);
                if (!linkedCts.IsCancellationRequested)
                    progress.OnRebuildSucceeded("Index rebuilt successfully.");
            }
            catch (OperationCanceledException)
            {
                // Superseded by a newer apply, or shutdown — no failure toast.
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
        }, CancellationToken.None);
    }
}
