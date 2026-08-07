using System.Runtime.CompilerServices;
using System.Text.Json;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Agent tool-chain integration: scripted LLM drives production Tools via
/// ToolRegistry.ReconfigureForProject against a synthetic temp project dir.
/// Distinct from <see cref="AgentSessionE2ETests"/> (orchestration-loop with FakeTool).
/// </summary>
public class AgentToolChainIntegrationTests : IDisposable
{
    private readonly string _tempDir;

    public AgentToolChainIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "IsaacAgentToolChain_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    // ── Scripted LLM (same idea as orchestration-loop tests) ──────

    private sealed class ScriptedChatService : IChatService
    {
        private readonly List<List<ChatChunk>> _turns;
        private int _callIndex;

        public int CallCount => _callIndex;
        public List<ChatRequest> ReceivedRequests { get; } = [];

        public ScriptedChatService(params List<ChatChunk>[] turns) => _turns = [.. turns];

        public Task<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatResponse
            {
                Message = new ChatMessage { Role = "assistant", Content = "ok" }
            });

        public async IAsyncEnumerable<ChatChunk> StreamAsync(
            ChatRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            ReceivedRequests.Add(request);
            await Task.Yield();
            var turn = _callIndex < _turns.Count ? _turns[_callIndex] : _turns[^1];
            _callIndex++;
            foreach (var chunk in turn)
                yield return chunk;
        }
    }

    private static ChatChunk TextChunk(string text) => new(text, false, -1, null, null, null);

    private static ChatChunk ToolCallChunk(int index, string id, string name, string args)
        => new("", true, index, id, name, args);

    private static ChatChunk ToolCallEnd(int index)
        => new("", true, index, null, null, null);

    private static string JsonArgs(object payload) => JsonSerializer.Serialize(payload);

    private static (AgentSession Session, ToolRegistry Registry) CreateSession(
        IChatService chat, string projectDir)
    {
        var logger = Mock.Of<ILogger<AgentSession>>();
        var toolLogger = Mock.Of<ILogger<ToolRegistry>>();
        // IRetriever null — knowledge tools out of scope for R-011
        var registry = new ToolRegistry(toolLogger, retriever: null);
        var session = new AgentSession(chat, registry, projectDir, logger);
        return (session, registry);
    }

    private const string ValidMetadataXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <metadata>
          <name>Test Mod</name>
          <directory>test_mod</directory>
          <description>A test mod</description>
          <version>1.0</version>
        </metadata>
        """;

    private const string InvalidMetadataXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <metadata>
          <name>Broken Mod</name>
          <directory>broken_mod</directory>
          <description>Missing version on purpose</description>
        </metadata>
        """;

    [Fact]
    public async Task ToolChain_HappyPath_WriteReadListValidateXml_ObservableOnDisk()
    {
        var metadataPath = "metadata.xml";
        var chat = new ScriptedChatService(
            // Iteration 0: write valid metadata.xml
            [
                ToolCallChunk(0, "c1", "write_file",
                    JsonArgs(new { path = metadataPath, content = ValidMetadataXml })),
                ToolCallEnd(0)
            ],
            // Iteration 1: read back
            [
                ToolCallChunk(0, "c2", "read_file",
                    JsonArgs(new { path = metadataPath })),
                ToolCallEnd(0)
            ],
            // Iteration 2: list project files
            [
                ToolCallChunk(0, "c3", "list_files", JsonArgs(new { })),
                ToolCallEnd(0)
            ],
            // Iteration 3: validate_xml
            [
                ToolCallChunk(0, "c4", "validate_xml",
                    JsonArgs(new { file_path = metadataPath })),
                ToolCallEnd(0)
            ],
            // Iteration 4: final reply
            [TextChunk("Tool chain complete: metadata is valid.")]
        );

        var (session, registry) = CreateSession(chat, _tempDir);
        Assert.Equal(Path.GetFullPath(_tempDir), Path.GetFullPath(registry.CurrentProjectDir!));
        Assert.NotNull(registry.Get("write_file"));
        Assert.NotNull(registry.Get("read_file"));
        Assert.NotNull(registry.Get("list_files"));
        Assert.NotNull(registry.Get("validate_xml"));

        var toolResults = new List<(string Result, string Name)>();
        session.OnToolResult += (result, name, _) => toolResults.Add((result, name));

        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("write and validate metadata"))
            output.Add(c);

        // Disk: write_file left observable content
        var onDisk = Path.Combine(_tempDir, metadataPath);
        Assert.True(File.Exists(onDisk));
        Assert.Contains("<name>Test Mod</name>", await File.ReadAllTextAsync(onDisk));

        // Tool results through the agent loop (production tools)
        Assert.Equal(4, toolResults.Count);
        Assert.Equal("write_file", toolResults[0].Name);
        Assert.Contains("File written", toolResults[0].Result);

        Assert.Equal("read_file", toolResults[1].Name);
        Assert.Contains("<name>Test Mod</name>", toolResults[1].Result);

        Assert.Equal("list_files", toolResults[2].Name);
        Assert.Contains(metadataPath, toolResults[2].Result.Replace('\\', '/'));

        Assert.Equal("validate_xml", toolResults[3].Name);
        Assert.Contains("is valid", toolResults[3].Result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("error(s)", toolResults[3].Result, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Tool chain complete: metadata is valid.", output);
        Assert.Equal(5, chat.CallCount);
    }

    [Fact]
    public async Task ToolChain_InvalidXml_ValidateXmlReportsFailureThroughLoop()
    {
        var metadataPath = "metadata.xml";
        var chat = new ScriptedChatService(
            [
                ToolCallChunk(0, "c1", "write_file",
                    JsonArgs(new { path = metadataPath, content = InvalidMetadataXml })),
                ToolCallEnd(0)
            ],
            [
                ToolCallChunk(0, "c2", "validate_xml",
                    JsonArgs(new { file_path = metadataPath })),
                ToolCallEnd(0)
            ],
            [TextChunk("Validation failed as expected.")]
        );

        var (session, _) = CreateSession(chat, _tempDir);
        var validateResults = new List<string>();
        session.OnToolResult += (result, name, _) =>
        {
            if (name == "validate_xml")
                validateResults.Add(result);
        };

        var output = new List<string>();
        await foreach (var c in session.SendMessageAsync("write broken metadata and validate"))
            output.Add(c);

        Assert.True(File.Exists(Path.Combine(_tempDir, metadataPath)));

        Assert.Single(validateResults);
        // Tool formats failures as "Found N error(s) ..."; avoid matching "No errors found."
        Assert.Contains("error(s)", validateResults[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("version", validateResults[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is valid. No errors found.", validateResults[0]);

        // Failure is also visible in history fed back to the LLM
        var toolMsg = session.History.Last(h => h.Role == "tool");
        Assert.Contains("error(s)", toolMsg.Content, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Validation failed as expected.", output);
        Assert.Equal(3, chat.CallCount);
    }
}
