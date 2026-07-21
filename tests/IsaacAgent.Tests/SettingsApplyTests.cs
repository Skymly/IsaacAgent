using Avalonia.Headless.XUnit;
using IsaacAgent.App.Services;
using IsaacAgent.App.ViewModels;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Settings apply seam (#15): chat swap + conditional Embedding apply + progress + cancel.
/// </summary>
[Collection("Avalonia")]
public class SettingsApplyTests
{
    private sealed class RecordingEmbeddingApply : IEmbeddingApply
    {
        private readonly object _lock = new();
        private TaskCompletionSource _started = NewTcs();
        private TaskCompletionSource _release = NewTcs();

        public int CallCount { get; private set; }
        public IEmbeddingProvider? LastProvider { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public Task Started
        {
            get { lock (_lock) return _started.Task; }
        }

        public bool BlockUntilReleased { get; set; }

        public async Task ApplyAsync(IEmbeddingProvider newProvider, CancellationToken ct = default)
        {
            TaskCompletionSource release;
            lock (_lock)
            {
                CallCount++;
                LastProvider = newProvider;
                LastToken = ct;
                _started.TrySetResult();
                release = _release;
            }

            if (BlockUntilReleased)
            {
                try
                {
                    using var reg = ct.Register(() => release.TrySetCanceled(ct));
                    await release.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
            }

            ct.ThrowIfCancellationRequested();
        }

        public void Release()
        {
            lock (_lock)
            {
                _release.TrySetResult();
            }
        }

        public void ResetGate()
        {
            lock (_lock)
            {
                _started = NewTcs();
                _release = NewTcs();
            }
        }

        private static TaskCompletionSource NewTcs()
            => new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingProgress : ISettingsApplyProgress
    {
        public int StartedCount { get; private set; }
        public int FinishedCount { get; private set; }
        public string? LastSuccess { get; private set; }
        public string? LastFailure { get; private set; }

        public void OnRebuildStarted() => StartedCount++;
        public void OnRebuildSucceeded(string status) => LastSuccess = status;
        public void OnRebuildFailed(string status) => LastFailure = status;
        public void OnRebuildFinished() => FinishedCount++;
    }

    private static ProviderIntent Intent(
        string model = "gpt",
        EmbeddingSourceType embeddingSource = EmbeddingSourceType.Onnx,
        string ollamaModel = "nomic-embed-text")
        => new(
            new ProviderConfig(ProviderType.OpenAICompatible, "https://api.example/v1", model, "key"),
            new EmbeddingConfig
            {
                Source = embeddingSource,
                OllamaEndpoint = "http://localhost:11434",
                OllamaModel = ollamaModel,
                OnnxModelPath = "",
                OnnxTokenizerPath = ""
            });

    private static (SettingsApply Apply, ChatServiceProxy ChatProxy, RecordingEmbeddingApply Embedding, List<ProviderConfig> ChatBuilds)
        CreateSut(EmbeddingConfig? initialEmbedding = null)
    {
        var chatProxy = new ChatServiceProxy(Mock.Of<IChatService>());
        var chatBuilds = new List<ProviderConfig>();
        IChatService BuildChat(ProviderConfig c)
        {
            chatBuilds.Add(c);
            return Mock.Of<IChatService>();
        }

        var embedding = new RecordingEmbeddingApply();
        IEmbeddingProvider BuildEmbedding(EmbeddingConfig _) => Mock.Of<IEmbeddingProvider>();

        var apply = new SettingsApply(
            chatProxy,
            BuildChat,
            embedding,
            BuildEmbedding,
            initialEmbedding ?? Intent().Embedding);

        return (apply, chatProxy, embedding, chatBuilds);
    }

    [Fact]
    public void Apply_LlmOnly_DoesNotCallEmbeddingApply()
    {
        var (apply, _, embedding, chatBuilds) = CreateSut();
        var progress = new RecordingProgress();

        var intent = Intent(model: "new-model");
        apply.Apply(intent, progress);

        Assert.Single(chatBuilds);
        Assert.Equal("new-model", chatBuilds[0].Model);
        Assert.Equal(0, embedding.CallCount);
        Assert.Equal(0, progress.StartedCount);
    }

    [Fact]
    public async Task Apply_EmbeddingChanged_StartsEmbeddingApplyWithoutWaiting()
    {
        var (apply, _, embedding, _) = CreateSut();
        embedding.BlockUntilReleased = true;
        var progress = new RecordingProgress();

        var intent = Intent(embeddingSource: EmbeddingSourceType.Ollama, ollamaModel: "other-model");
        apply.Apply(intent, progress);

        Assert.Equal(1, progress.StartedCount);
        await embedding.Started.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, embedding.CallCount);
        Assert.Null(progress.LastSuccess); // not finished yet

        embedding.Release();
        await WaitUntilAsync(() => progress.FinishedCount == 1, TimeSpan.FromSeconds(2));
        Assert.NotNull(progress.LastSuccess);
    }

    [Fact]
    public async Task Apply_SecondEmbeddingSave_CancelsInFlightRebuild()
    {
        var (apply, _, embedding, _) = CreateSut();
        embedding.BlockUntilReleased = true;
        var progress1 = new RecordingProgress();
        var progress2 = new RecordingProgress();

        apply.Apply(Intent(ollamaModel: "model-a", embeddingSource: EmbeddingSourceType.Ollama), progress1);
        await embedding.Started.WaitAsync(TimeSpan.FromSeconds(2));
        var firstToken = embedding.LastToken;

        embedding.ResetGate();
        apply.Apply(Intent(ollamaModel: "model-b", embeddingSource: EmbeddingSourceType.Ollama), progress2);
        await embedding.Started.WaitAsync(TimeSpan.FromSeconds(2));

        await WaitUntilAsync(() => firstToken.IsCancellationRequested, TimeSpan.FromSeconds(2));
        Assert.True(firstToken.IsCancellationRequested);
        Assert.Equal(1, progress1.StartedCount);
        Assert.Equal(1, progress2.StartedCount);
        Assert.Equal(2, embedding.CallCount);
    }

    [Fact]
    public async Task Apply_EmbeddingRebuildFails_ReportsFailureViaProgress()
    {
        var chatProxy = new ChatServiceProxy(Mock.Of<IChatService>());
        var failing = new Mock<IEmbeddingApply>();
        failing
            .Setup(e => e.ApplyAsync(It.IsAny<IEmbeddingProvider>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var apply = new SettingsApply(
            chatProxy,
            _ => Mock.Of<IChatService>(),
            failing.Object,
            _ => Mock.Of<IEmbeddingProvider>(),
            Intent().Embedding);

        var progress = new RecordingProgress();
        var ollamaIntent = Intent(embeddingSource: EmbeddingSourceType.Ollama);
        apply.Apply(ollamaIntent, progress);

        await WaitUntilAsync(() => progress.LastFailure is not null, TimeSpan.FromSeconds(2));
        Assert.Contains("boom", progress.LastFailure);
        Assert.Equal(1, progress.FinishedCount);

        // Same embedding intent can retry after failure.
        failing.Invocations.Clear();
        failing
            .Setup(e => e.ApplyAsync(It.IsAny<IEmbeddingProvider>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var progress2 = new RecordingProgress();
        apply.Apply(ollamaIntent, progress2);
        await WaitUntilAsync(() => progress2.FinishedCount == 1, TimeSpan.FromSeconds(2));
        Assert.NotNull(progress2.LastSuccess);
    }

    [AvaloniaFact]
    public async Task SettingsApplyProgress_UpdatesSettingsFlagsAndStatus()
    {
        var vm = new SettingsViewModel(new AppConfiguration());
        var progress = new SettingsApplyProgress(vm);

        progress.OnRebuildStarted();
        AvaloniaTestHelper.FlushDispatcher();
        Assert.True(vm.IsRebuildingIndex);
        Assert.Equal("Building knowledge index...", vm.IndexStatus);

        progress.OnRebuildSucceeded("Index rebuilt successfully.");
        progress.OnRebuildFinished();
        AvaloniaTestHelper.FlushDispatcher();
        Assert.False(vm.IsRebuildingIndex);
        Assert.Equal("Index rebuilt successfully.", vm.IndexStatus);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10);
        }

        Assert.True(condition(), "Condition not met before timeout.");
    }
}
