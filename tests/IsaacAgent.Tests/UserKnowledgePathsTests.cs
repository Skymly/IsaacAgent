using IsaacAgent.Rag.Indexing;
using Xunit;

namespace IsaacAgent.Tests;

public class UserKnowledgePathsTests
{
    [Fact]
    public void ResolveDirectory_IsSiblingKnowledgeUnderIsaacAgentRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "IsaacAgentRoot");
        var path = UserKnowledgePaths.ResolveDirectory(root);
        Assert.Equal(Path.Combine(root, "knowledge"), path);
    }

    [Fact]
    public void ResolveLegacyExamplesDirectory_IsUnderRagDataDir()
    {
        var rag = Path.Combine(Path.GetTempPath(), "rag");
        Assert.Equal(Path.Combine(rag, "examples"), UserKnowledgePaths.ResolveLegacyExamplesDirectory(rag));
    }

    [Fact]
    public void EnsurePrepared_CreatesKnowledgeDirectory()
    {
        var root = CreateTempRoot();
        try
        {
            var knowledge = UserKnowledgePaths.ResolveDirectory(root);
            var legacy = UserKnowledgePaths.ResolveLegacyExamplesDirectory(Path.Combine(root, "rag"));

            var result = UserKnowledgePaths.EnsurePrepared(knowledge, legacy);

            Assert.Equal(knowledge, result);
            Assert.True(Directory.Exists(knowledge));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnsurePrepared_MovesLegacyExamplesWhenKnowledgeEmpty()
    {
        var root = CreateTempRoot();
        try
        {
            var knowledge = UserKnowledgePaths.ResolveDirectory(root);
            var legacy = UserKnowledgePaths.ResolveLegacyExamplesDirectory(Path.Combine(root, "rag"));
            Directory.CreateDirectory(Path.Combine(legacy, "nested"));
            File.WriteAllText(Path.Combine(legacy, "nested", "note.md"), "# Hello");

            UserKnowledgePaths.EnsurePrepared(knowledge, legacy);

            Assert.True(File.Exists(Path.Combine(knowledge, "nested", "note.md")));
            Assert.Equal("# Hello", File.ReadAllText(Path.Combine(knowledge, "nested", "note.md")));
            Assert.False(Directory.Exists(legacy));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnsurePrepared_LeavesLegacyAloneWhenKnowledgeHasFiles()
    {
        var root = CreateTempRoot();
        try
        {
            var knowledge = UserKnowledgePaths.ResolveDirectory(root);
            var legacy = UserKnowledgePaths.ResolveLegacyExamplesDirectory(Path.Combine(root, "rag"));
            Directory.CreateDirectory(knowledge);
            Directory.CreateDirectory(legacy);
            File.WriteAllText(Path.Combine(knowledge, "keep.md"), "# Keep");
            File.WriteAllText(Path.Combine(legacy, "orphan.md"), "# Orphan");

            UserKnowledgePaths.EnsurePrepared(knowledge, legacy);

            Assert.True(File.Exists(Path.Combine(knowledge, "keep.md")));
            Assert.False(File.Exists(Path.Combine(knowledge, "orphan.md")));
            Assert.True(File.Exists(Path.Combine(legacy, "orphan.md")));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void EnsurePrepared_RollsBackPartialMoveSoRetryCanComplete()
    {
        var root = CreateTempRoot();
        try
        {
            var knowledge = UserKnowledgePaths.ResolveDirectory(root);
            var legacy = UserKnowledgePaths.ResolveLegacyExamplesDirectory(Path.Combine(root, "rag"));
            Directory.CreateDirectory(knowledge);
            Directory.CreateDirectory(legacy);
            // Directory named like a legacy file blocks that move after others may succeed.
            Directory.CreateDirectory(Path.Combine(knowledge, "second.md"));
            File.WriteAllText(Path.Combine(legacy, "first.md"), "1");
            File.WriteAllText(Path.Combine(legacy, "second.md"), "2");

            Assert.ThrowsAny<IOException>(() => UserKnowledgePaths.EnsurePrepared(knowledge, legacy));

            Assert.Empty(Directory.EnumerateFiles(knowledge, "*", SearchOption.AllDirectories));
            Assert.True(File.Exists(Path.Combine(legacy, "first.md")));
            Assert.True(File.Exists(Path.Combine(legacy, "second.md")));

            Directory.Delete(Path.Combine(knowledge, "second.md"));
            UserKnowledgePaths.EnsurePrepared(knowledge, legacy);

            Assert.True(File.Exists(Path.Combine(knowledge, "first.md")));
            Assert.True(File.Exists(Path.Combine(knowledge, "second.md")));
            Assert.False(Directory.Exists(legacy));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), $"isaac_user_knowledge_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }
}
