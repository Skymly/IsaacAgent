using IsaacAgent.Core.PathSafety;
using Xunit;

namespace IsaacAgent.Tests;

public class ProjectPathSafetyTests
{
    [Fact]
    public void Resolve_ValidRelativePath_IsSafe()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), $"pps_valid_{Guid.NewGuid():N}");
        Directory.CreateDirectory(projectDir);
        try
        {
            var (fullPath, isSafe) = ProjectPathSafety.Resolve(projectDir, "main.lua");

            Assert.True(isSafe);
            Assert.Equal(Path.GetFullPath(Path.Combine(projectDir, "main.lua")), fullPath);
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void Resolve_ParentTraversal_IsUnsafe()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"pps_trav_{Guid.NewGuid():N}");
        var projectDir = Path.Combine(baseDir, "myproject");
        Directory.CreateDirectory(projectDir);
        try
        {
            var (_, isSafe) = ProjectPathSafety.Resolve(projectDir, "../outside.lua");

            Assert.False(isSafe);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void Resolve_SiblingPrefix_IsUnsafe()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"pps_sib_{Guid.NewGuid():N}");
        var projectDir = Path.Combine(baseDir, "myproject");
        var siblingDir = Path.Combine(baseDir, "myproject_evil");
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(siblingDir);
        try
        {
            var (fullPath, isSafe) = ProjectPathSafety.Resolve(projectDir, "../myproject_evil/secret.lua");

            Assert.False(isSafe);
            Assert.Equal(Path.GetFullPath(Path.Combine(siblingDir, "secret.lua")), fullPath);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void Resolve_DoubleEncodedDots_IsUnsafe()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"pps_dots_{Guid.NewGuid():N}");
        var projectDir = Path.Combine(baseDir, "myproject");
        Directory.CreateDirectory(projectDir);
        try
        {
            var (_, isSafe) = ProjectPathSafety.Resolve(projectDir, "....//outside.lua");

            Assert.False(isSafe);
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void IsWithinProject_ExactRoot_IsTrue()
    {
        var projectDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pps_root_{Guid.NewGuid():N}"));
        Directory.CreateDirectory(projectDir);
        try
        {
            Assert.True(ProjectPathSafety.IsWithinProject(projectDir, projectDir));
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void IsWithinProject_SiblingPrefixPath_IsFalse()
    {
        var baseDir = Path.Combine(Path.GetTempPath(), $"pps_sib2_{Guid.NewGuid():N}");
        var projectDir = Path.GetFullPath(Path.Combine(baseDir, "myproject"));
        var siblingFile = Path.GetFullPath(Path.Combine(baseDir, "myproject_evil", "secret.lua"));
        Directory.CreateDirectory(projectDir);
        Directory.CreateDirectory(Path.GetDirectoryName(siblingFile)!);
        try
        {
            Assert.False(ProjectPathSafety.IsWithinProject(siblingFile, projectDir));
        }
        finally
        {
            Directory.Delete(baseDir, true);
        }
    }

    [Fact]
    public void GetDefaultIsaacLogPath_UsesDocumentsMyGamesRepentance()
    {
        var expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "My Games",
            "Binding of Isaac Repentance",
            "log.txt");

        Assert.Equal(expected, ProjectPathSafety.GetDefaultIsaacLogPath());
    }

    [Fact]
    public void IsAllowedAbsoluteLogPath_DefaultLocation_IsTrue()
    {
        var defaultLog = ProjectPathSafety.GetDefaultIsaacLogPath();

        Assert.True(ProjectPathSafety.IsAllowedAbsoluteLogPath(defaultLog));
    }

    [Fact]
    public void IsAllowedAbsoluteLogPath_UnrelatedAbsolutePath_IsFalse()
    {
        var other = Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"pps_other_{Guid.NewGuid():N}.txt"));

        Assert.False(ProjectPathSafety.IsAllowedAbsoluteLogPath(other));
    }

    [Fact]
    public void IsAllowedAbsoluteLogPath_RelativePath_IsFalse()
    {
        Assert.False(ProjectPathSafety.IsAllowedAbsoluteLogPath("log.txt"));
        Assert.False(ProjectPathSafety.IsAllowedAbsoluteLogPath(Path.Combine("My Games", "Binding of Isaac Repentance", "log.txt")));
    }

    [Fact]
    public void IsAllowedAbsoluteLogPath_DefaultLocationCaseInsensitive_IsTrue()
    {
        var defaultLog = ProjectPathSafety.GetDefaultIsaacLogPath();
        var varied = defaultLog.ToUpperInvariant();

        Assert.True(ProjectPathSafety.IsAllowedAbsoluteLogPath(varied));
    }
}
