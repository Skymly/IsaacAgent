using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using IsaacAgent.LLM;
using IsaacAgent.Rag.Embedding;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;
using IsaacAgent.Rag.Tools;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class IntegrationTests
{
    private sealed class StubEmbeddingProvider : IEmbeddingProvider
    {
        public string ModelName { get; set; } = "test-model";
        public int Dimensions { get; set; } = 3;

        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new float[] { 1f, 0f, 0f });

        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new float[] { 1f, 0f, 0f }).ToList());
    }

    private sealed class StubChatService : IChatService
    {
        private readonly ChatChunk[] _chunks;
        public int StreamCallCount { get; private set; }

        public StubChatService(params ChatChunk[] chunks) => _chunks = chunks;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse { Message = new ChatMessage { Role = "assistant", Content = "ok" } });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            StreamCallCount++;
            await Task.Yield();
            foreach (var c in _chunks)
                yield return c;
        }
    }

    private sealed class MultiPhaseChatService : IChatService
    {
        private readonly ChatChunk[][] _phases;
        private int _callCount;

        public MultiPhaseChatService(params ChatChunk[][] phases) => _phases = phases;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse { Message = new ChatMessage { Role = "assistant", Content = "ok" } });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            var phase = _callCount < _phases.Length ? _phases[_callCount] : _phases[^1];
            _callCount++;
            await Task.Yield();
            foreach (var c in phase)
                yield return c;
        }
    }

    private sealed class FailOnceChatService : IChatService
    {
        private readonly ChatChunk[] _chunks;
        public int CallCount { get; private set; }

        public FailOnceChatService(params ChatChunk[] chunks) => _chunks = chunks;

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public async IAsyncEnumerable<ChatChunk> StreamAsync(ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            CallCount++;
            await Task.Yield();
            if (CallCount == 1)
                throw new HttpRequestException("Transient failure");
            foreach (var c in _chunks)
                yield return c;
        }
    }

    private sealed class ThrowingTool : ITool
    {
        public string Name => "throwing_tool";
        public string Description => "A tool that always throws";
        public ToolDefinition Definition => new() { Name = Name, Description = Description, Parameters = new ToolParameters() };

        public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
            => throw new InvalidOperationException("Tool exploded");
    }

    private sealed class EchoTool : ITool
    {
        public string Name => "echo_tool";
        public string Description => "Echoes back the arguments";
        public ToolDefinition Definition => new() { Name = Name, Description = Description, Parameters = new ToolParameters() };

        public Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
            => Task.FromResult($"Echo: {arguments}");
    }

    private static AgentSession CreateSession(IChatService chat, ToolRegistry? tools = null)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        tools ??= new ToolRegistry(toolLogger);
        return new AgentSession(chat, tools, null, logger);
    }

    private static IndexBuilder CreateBuilder(IEmbeddingProvider embedding, InMemoryVectorStore store, out string tempDir)
    {
        tempDir = Path.Combine(Path.GetTempPath(), $"isaac_integration_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return new IndexBuilder(embedding, store, tempDir, Mock.Of<ILogger<IndexBuilder>>());
    }

    [Fact]
    public async Task AgentSession_WithRetryChatService_RetriesAndCompletes()
    {
        var inner = new FailOnceChatService(
            new ChatChunk("Recovered after retry", false, -1, null, null, null));

        var retryService = new RetryChatService(
            inner, maxRetries: 3,
            new[] { TimeSpan.FromMilliseconds(10) },
            Mock.Of<ILogger<RetryChatService>>());

        var session = CreateSession(retryService);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("test"))
            chunks.Add(c);

        Assert.Equal(2, inner.CallCount);
        Assert.Contains("Recovered after retry", chunks);
    }

    [Fact]
    public async Task AgentSession_ParallelTools_OneThrows_OtherSucceeds_BothResultsInHistory()
    {
        var chat = new MultiPhaseChatService(
            new ChatChunk[]
            {
                new("", true, 0, "call_1", "throwing_tool", """{}"""),
                new("", true, 0, null, null, null),
                new("", true, 1, "call_2", "echo_tool", """{"msg":"hi"}"""),
                new("", true, 1, null, null, null),
            },
            new ChatChunk[]
            {
                new("Done after mixed tools", false, -1, null, null, null),
            });

        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        var tools = new ToolRegistry(toolLogger);

        var session = CreateSession(chat, tools);
        tools.Register(new ThrowingTool());
        tools.Register(new EchoTool());

        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("test"))
            chunks.Add(c);

        Assert.Contains("Done after mixed tools", chunks);

        var toolMessages = session.History.Where(h => h.Role == "tool").ToList();
        Assert.Equal(2, toolMessages.Count);

        Assert.Contains("Error", toolMessages[0].Content);
        Assert.Contains("throwing_tool", toolMessages[0].Content);

        Assert.Contains("Echo", toolMessages[1].Content);
        Assert.Contains("hi", toolMessages[1].Content);
    }

    [Fact]
    public async Task EmbeddingProviderProxy_HotReload_RetrieverRebuilds_SearchWorks()
    {
        var embedding1 = new StubEmbeddingProvider { ModelName = "model-a", Dimensions = 3 };
        var store = new InMemoryVectorStore();
        var proxy = new EmbeddingProviderProxy(embedding1);
        var indexPath = Path.Combine(Path.GetTempPath(), $"isaac_integration_{Guid.NewGuid():N}.bin");
        var builder = CreateBuilder(proxy, store, out var tempDir);

        try
        {
            var retriever = new Retriever(proxy, store, builder, indexPath, Mock.Of<ILogger<Retriever>>());

            await retriever.RebuildIndexAsync();
            Assert.True(retriever.IsReady);
            Assert.Equal("model-a", store.ModelName);
            Assert.True(store.Count > 0);

            var searchTool = new SearchKnowledgeTool(retriever);
            var result = await searchTool.ExecuteAsync("""{"query":"test"}""");
            Assert.Contains("Found", result);

            var embedding2 = new StubEmbeddingProvider { ModelName = "model-b", Dimensions = 3 };
            proxy.Replace(embedding2);
            retriever.ResetReady();
            await retriever.RebuildIndexAsync();

            Assert.True(retriever.IsReady);
            Assert.Equal("model-b", store.ModelName);

            var result2 = await searchTool.ExecuteAsync("""{"query":"test"}""");
            Assert.Contains("Found", result2);
        }
        finally
        {
            if (File.Exists(indexPath)) File.Delete(indexPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task AgentSession_WithRagTools_SearchKnowledgeReturnsResults()
    {
        var embedding = new StubEmbeddingProvider { ModelName = "test-model", Dimensions = 3 };
        var store = new InMemoryVectorStore();
        var indexPath = Path.Combine(Path.GetTempPath(), $"isaac_integration_{Guid.NewGuid():N}.bin");
        var builder = CreateBuilder(embedding, store, out var tempDir);

        try
        {
            var retriever = new Retriever(embedding, store, builder, indexPath, Mock.Of<ILogger<Retriever>>());
            await retriever.RebuildIndexAsync();

            var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
            var tools = new ToolRegistry(toolLogger, retriever);
            tools.ReconfigureForProject(null);

            var chat = new StubChatService(
                new ChatChunk("", true, 0, "call_1", "search_knowledge", """{"query":"MC_POST_UPDATE"}"""),
                new ChatChunk("", true, 0, null, null, null),
                new ChatChunk("Found callback info", false, -1, null, null, null));

            var session = CreateSession(chat, tools);
            var chunks = new List<string>();
            await foreach (var c in session.SendMessageAsync("search for MC_POST_UPDATE"))
                chunks.Add(c);

            Assert.Contains("Found callback info", chunks);

            var toolMsg = session.History.FirstOrDefault(h => h.Role == "tool");
            Assert.NotNull(toolMsg);
            Assert.Contains("MC_POST_UPDATE", toolMsg!.Content);
        }
        finally
        {
            if (File.Exists(indexPath)) File.Delete(indexPath);
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task ChatServiceProxy_HotReload_SwitchesUnderlyingProvider()
    {
        var initial = new StubChatService(
            new ChatChunk("From initial provider", false, -1, null, null, null));

        var proxy = new ChatServiceProxy(initial);

        var session = CreateSession(proxy);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("test"))
            chunks.Add(c);

        Assert.Contains("From initial provider", chunks);

        var replacement = new StubChatService(
            new ChatChunk("From replacement provider", false, -1, null, null, null));

        proxy.Replace(replacement);

        session.ClearHistory();
        chunks.Clear();
        await foreach (var c in session.SendMessageAsync("test2"))
            chunks.Add(c);

        Assert.Contains("From replacement provider", chunks);
        Assert.DoesNotContain("From initial provider", chunks);
    }

    [Fact]
    public async Task AgentSession_FullConversation_TextThenToolCallThenText_HistoryCorrect()
    {
        var chat = new MultiPhaseChatService(
            new ChatChunk[]
            {
                new("Let me search for that.", false, -1, null, null, null),
                new("", true, 0, "call_1", "search_isaac_api", """{"query":"EntityPlayer","category":"class"}"""),
                new("", true, 0, null, null, null),
            },
            new ChatChunk[]
            {
                new("Here's what I found about EntityPlayer.", false, -1, null, null, null),
            });

        var session = CreateSession(chat);
        var chunks = new List<string>();
        await foreach (var c in session.SendMessageAsync("tell me about EntityPlayer"))
            chunks.Add(c);

        Assert.Equal(2, chunks.Count);
        Assert.Contains("Let me search", chunks[0]);
        Assert.Contains("Here's what I found", chunks[1]);

        Assert.True(session.History.Count >= 5);
        Assert.Equal("user", session.History[1].Role);
        Assert.Equal("assistant", session.History[2].Role);
        Assert.Equal("tool", session.History[3].Role);
        Assert.Equal("assistant", session.History[4].Role);
    }
}
