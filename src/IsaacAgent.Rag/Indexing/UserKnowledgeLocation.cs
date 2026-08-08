namespace IsaacAgent.Rag.Indexing;

/// <summary>
/// DI-accessible path to the prepared User knowledge directory.
/// App Settings binds this instead of hardcoding AppData layout.
/// </summary>
public sealed class UserKnowledgeLocation
{
    public UserKnowledgeLocation(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        DirectoryPath = directoryPath;
    }

    public string DirectoryPath { get; }
}
