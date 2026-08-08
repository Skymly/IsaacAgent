namespace IsaacAgent.Rag.Indexing;

/// <summary>
/// Resolves the app-global User knowledge directory and one-time migration from the
/// legacy <c>rag/examples</c> folder.
/// </summary>
public static class UserKnowledgePaths
{
    /// <summary>Chunk <see cref="Core.Models.KnowledgeChunk.Source"/> for User knowledge.</summary>
    public const string SourceId = "user";

    /// <summary>
    /// Default User knowledge root: <c>%APPDATA%\IsaacAgent\knowledge</c> (sibling of <c>rag/</c>).
    /// </summary>
    public static string ResolveDirectory(string isaacAgentRoot)
        => Path.Combine(isaacAgentRoot, "knowledge");

    /// <summary>Legacy directory previously scanned as filesystem examples.</summary>
    public static string ResolveLegacyExamplesDirectory(string ragDataDir)
        => Path.Combine(ragDataDir, "examples");

    /// <summary>
    /// Ensures <paramref name="knowledgeDir"/> exists. When it is empty and
    /// <paramref name="legacyExamplesDir"/> has files, moves that content into knowledge
    /// and best-effort removes the empty legacy directory. If knowledge already has
    /// content, leaves an orphaned legacy directory alone.
    /// </summary>
    public static string EnsurePrepared(string knowledgeDir, string legacyExamplesDir)
    {
        Directory.CreateDirectory(knowledgeDir);

        if (!IsEmptyOfFiles(knowledgeDir))
            return knowledgeDir;

        if (!Directory.Exists(legacyExamplesDir) || IsEmptyOfFiles(legacyExamplesDir))
            return knowledgeDir;

        MoveDirectoryContents(legacyExamplesDir, knowledgeDir);
        TryDeleteEmptyDirectoryTree(legacyExamplesDir);

        return knowledgeDir;
    }

    private static bool IsEmptyOfFiles(string directory)
    {
        if (!Directory.Exists(directory))
            return true;

        return !Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Any();
    }

    private static void MoveDirectoryContents(string sourceDir, string destDir)
    {
        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, directory);
            Directory.CreateDirectory(Path.Combine(destDir, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var destFile = Path.Combine(destDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
            File.Move(file, destFile, overwrite: false);
        }
    }

    private static void TryDeleteEmptyDirectoryTree(string directory)
    {
        if (!Directory.Exists(directory))
            return;

        try
        {
            foreach (var child in Directory.EnumerateDirectories(directory))
                TryDeleteEmptyDirectoryTree(child);

            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch (IOException)
        {
            // Best-effort cleanup of the legacy folder.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of the legacy folder.
        }
    }
}
