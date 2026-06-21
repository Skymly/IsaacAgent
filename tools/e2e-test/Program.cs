using IsaacAgent.Core.Services;
using IsaacAgent.Rag;
using IsaacAgent.Rag.Embedding;
using IsaacAgent.Rag.Indexing;
using IsaacAgent.Rag.Retrieval;
using IsaacAgent.Rag.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Set up DI
var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));

var embeddingConfig = new EmbeddingConfig
{
    Source = EmbeddingSourceType.Ollama,
    OllamaEndpoint = "http://localhost:11434",
    OllamaModel = "nomic-embed-text",
};

var tempDir = Path.Combine(Path.GetTempPath(), "isaac_rag_e2e");
if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
Directory.CreateDirectory(tempDir);

services.AddRag(embeddingConfig, tempDir);

var sp = services.BuildServiceProvider();
var retriever = sp.GetRequiredService<IRetriever>();
var store = sp.GetRequiredService<InMemoryVectorStore>();

Console.WriteLine("=== Building RAG index (this may take a while) ===");
var sw = System.Diagnostics.Stopwatch.StartNew();

await retriever.EnsureIndexAsync();
sw.Stop();

Console.WriteLine($"Index built in {sw.Elapsed.TotalSeconds:F1}s");
Console.WriteLine($"Total entries: {store.Count}");
Console.WriteLine($"Dimensions: {store.Dimensions}");
Console.WriteLine($"Model: {store.ModelName}");

Console.WriteLine("\n=== Running test queries ===");

var queries = new[]
{
    "How to create a custom collectible item",
    "MC_POST_UPDATE callback usage",
    "EntityPlayer methods for health",
    "items.xml configuration attributes",
    "REPENTOGON ImGui custom UI",
};

foreach (var query in queries)
{
    Console.WriteLine($"\nQuery: \"{query}\"");
    var results = await retriever.SearchAsync(query, topK: 3);
    foreach (var r in results)
    {
        Console.WriteLine($"  [{r.Score:F3}] [{r.Chunk.Source}/{r.Chunk.Category}] {r.Chunk.Title}");
        var preview = r.Chunk.Content.Length > 120 ? r.Chunk.Content[..120] + "..." : r.Chunk.Content;
        Console.WriteLine($"    {preview.Replace('\n', ' ')}");
    }
}

Console.WriteLine("\n=== Verifying index persistence ===");
var indexPath = Path.Combine(tempDir, "index.bin");
Console.WriteLine($"Index file exists: {File.Exists(indexPath)}");
if (File.Exists(indexPath))
    Console.WriteLine($"Index file size: {new FileInfo(indexPath).Length / 1024} KB");

// Test reload
Console.WriteLine("\n=== Testing reload from disk ===");
var store2 = new InMemoryVectorStore();
await store2.LoadAsync(indexPath);
Console.WriteLine($"Reloaded entries: {store2.Count}");
Console.WriteLine($"Reloaded model: {store2.ModelName}");

var reloadResults = store2.Search(
    (await sp.GetRequiredService<IEmbeddingProvider>().EmbedAsync("custom item creation")),
    topK: 2);
Console.WriteLine($"Reload search results: {reloadResults.Count}");
foreach (var r in reloadResults)
    Console.WriteLine($"  [{r.Score:F3}] {r.Chunk.Title}");

Console.WriteLine("\n=== Done ===");
