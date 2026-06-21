using IsaacAgent.Core.Knowledge;
using IsaacAgent.Core.Models;

namespace IsaacAgent.Rag.Chunking;

public static class ApiDocChunker
{
    public static List<KnowledgeChunk> ChunkFromKnowledge()
    {
        var chunks = new List<KnowledgeChunk>();

        foreach (var (name, info) in ModCallbacks.Callbacks)
        {
            chunks.Add(new KnowledgeChunk
            {
                Id = $"callback:{name}",
                Source = "vanilla",
                Category = "callback",
                Title = name,
                Content = $"Callback: {name} (ID: {info.Id})\nArguments: {info.Args}\nOptionalArgs: {info.OptionalArgs}\nDescription: {info.Description}",
                Metadata = { ["id"] = info.Id.ToString() }
            });
        }

        foreach (var (name, info) in IsaacClasses.Classes)
        {
            var methodsText = string.Join('\n', info.Methods.Select(m => $"  - {m}"));
            chunks.Add(new KnowledgeChunk
            {
                Id = $"class:{name}",
                Source = "vanilla",
                Category = "class",
                Title = name,
                Content = $"Class: {name}\nCategory: {info.Category}\nDescription: {info.Description}\nMethods:\n{methodsText}",
                Metadata = { ["category"] = info.Category }
            });
        }

        foreach (var (name, info) in IsaacEnums.Enums)
        {
            var valuesText = string.Join('\n', info.Values.Select(v => $"  {v}"));
            chunks.Add(new KnowledgeChunk
            {
                Id = $"enum:{name}",
                Source = "vanilla",
                Category = "enum",
                Title = name,
                Content = $"Enum: {name}\nDescription: {info.Description}\nValues:\n{valuesText}",
                Metadata = { ["valueCount"] = info.Values.Length.ToString() }
            });
        }

        return chunks;
    }
}
