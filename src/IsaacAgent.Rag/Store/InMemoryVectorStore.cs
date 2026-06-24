using System.IO;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Store;

public sealed class VectorStoreEntry
{
    public required KnowledgeChunk Chunk { get; init; }
    public required float[] Vector { get; init; }
}

public sealed class VectorStoreIndex
{
    public required string ModelName { get; init; }
    public required int Dimensions { get; init; }
    public required DateTimeOffset BuiltAt { get; init; }
    public required List<VectorStoreEntry> Entries { get; set; }
}

public sealed class InMemoryVectorStore
{
    private readonly object _lock = new();
    private List<VectorStoreEntry> _entries = [];
    private string _modelName = "";
    private int _dimensions;
    private DateTimeOffset _builtAt;

    public string ModelName => _modelName;
    public int Dimensions => _dimensions;
    public int Count { get { lock (_lock) return _entries.Count; } }
    public DateTimeOffset BuiltAt => _builtAt;

    public void ReplaceAll(string modelName, int dimensions, IEnumerable<VectorStoreEntry> entries)
    {
        lock (_lock)
        {
            _modelName = modelName;
            _dimensions = dimensions;
            _entries = entries.ToList();
            _builtAt = DateTimeOffset.UtcNow;
        }
    }

    public void AddRange(IEnumerable<VectorStoreEntry> entries)
    {
        lock (_lock)
            _entries.AddRange(entries);
    }

    public IReadOnlyList<RetrievalResult> Search(float[] queryVector, int topK, string? categoryFilter = null)
    {
        // Take a defensive copy so concurrent AddRange/ReplaceAll calls
        // don't mutate the list while we iterate it outside the lock.
        List<VectorStoreEntry> snapshot;
        lock (_lock)
            snapshot = _entries.ToList();

        if (snapshot.Count == 0 || queryVector.Length != _dimensions)
            return [];

        var scores = new List<(VectorStoreEntry Entry, float Score)>(snapshot.Count);
        foreach (var entry in snapshot)
        {
            if (categoryFilter is not null && entry.Chunk.Category != categoryFilter)
                continue;
            var score = CosineSimilarity(queryVector, entry.Vector);
            scores.Add((entry, score));
        }

        return scores
            .OrderByDescending(s => s.Score)
            .Take(topK)
            .Select(s => new RetrievalResult { Chunk = s.Entry.Chunk, Score = s.Score })
            .ToList();
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        var dot = 0f;
        var normA = 0f;
        var normB = 0f;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = MathF.Sqrt(normA) * MathF.Sqrt(normB);
        return denom > 0 ? dot / denom : 0f;
    }

    public VectorStoreIndex ExportIndex()
    {
        List<VectorStoreEntry> entries;
        lock (_lock)
            entries = _entries.ToList();

        return new VectorStoreIndex
        {
            ModelName = _modelName,
            Dimensions = _dimensions,
            BuiltAt = _builtAt,
            Entries = entries
        };
    }

    public void ImportIndex(VectorStoreIndex index)
    {
        lock (_lock)
        {
            _modelName = index.ModelName;
            _dimensions = index.Dimensions;
            _builtAt = index.BuiltAt;
            _entries = index.Entries;
        }
    }

    private const uint IndexFormatVersion = 1;

    public async Task SaveAsync(string path, CancellationToken ct = default)
    {
        var index = ExportIndex();
        await using var fs = File.Create(path);
        using var writer = new BinaryWriter(fs);
        writer.Write(IndexFormatVersion);
        writer.Write(index.ModelName);
        writer.Write(index.Dimensions);
        writer.Write(index.BuiltAt.ToUnixTimeSeconds());
        writer.Write(index.Entries.Count);
        foreach (var entry in index.Entries)
        {
            writer.Write(entry.Chunk.Id);
            writer.Write(entry.Chunk.Source);
            writer.Write(entry.Chunk.Category);
            writer.Write(entry.Chunk.Title);
            writer.Write(entry.Chunk.Content);
            writer.Write(entry.Chunk.Metadata.Count);
            foreach (var (k, v) in entry.Chunk.Metadata)
            {
                writer.Write(k);
                writer.Write(v);
            }
            writer.Write(entry.Vector.Length);
            foreach (var v in entry.Vector)
                writer.Write(v);
        }
        await fs.FlushAsync(ct);
    }

    public async Task<bool> LoadAsync(string path, CancellationToken ct = default)
    {
        if (!File.Exists(path)) return false;

        try
        {
            await using var fs = File.OpenRead(path);
            using var reader = new BinaryReader(fs);

            var version = reader.ReadUInt32();
            if (version != IndexFormatVersion)
                return false; // Incompatible format — will rebuild

            var index = new VectorStoreIndex
            {
                ModelName = reader.ReadString(),
                Dimensions = reader.ReadInt32(),
                BuiltAt = DateTimeOffset.FromUnixTimeSeconds(reader.ReadInt64()),
                Entries = []
            };

            var count = reader.ReadInt32();
            var entries = new List<VectorStoreEntry>(count);
            for (var i = 0; i < count; i++)
            {
                var chunk = new KnowledgeChunk
                {
                    Id = reader.ReadString(),
                    Source = reader.ReadString(),
                    Category = reader.ReadString(),
                    Title = reader.ReadString(),
                    Content = reader.ReadString(),
                    Metadata = { }
                };
                var metaCount = reader.ReadInt32();
                for (var m = 0; m < metaCount; m++)
                    chunk.Metadata[reader.ReadString()] = reader.ReadString();

                var vecLen = reader.ReadInt32();
                var vector = new float[vecLen];
                for (var v = 0; v < vecLen; v++)
                    vector[v] = reader.ReadSingle();

                entries.Add(new VectorStoreEntry { Chunk = chunk, Vector = vector });
            }
            index.Entries = entries;

            ImportIndex(index);
            return true;
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or System.Text.Json.JsonException or ArgumentOutOfRangeException)
        {
            // Corrupted or incompatible index file — treat as missing.
            return false;
        }
    }
}
