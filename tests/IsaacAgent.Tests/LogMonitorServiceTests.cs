using IsaacAgent.App.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace IsaacAgent.Tests;

public class LogMonitorServiceTests
{
    [Fact]
    public void ParseLine_LuaError_ParsesSourceFileAndLine()
    {
        var entry = LogMonitorService.ParseLine("main.lua:42: attempt to index nil value", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
        Assert.Equal("main.lua", entry.SourceFile);
        Assert.Equal(42, entry.SourceLine);
        Assert.Equal(1, entry.LineNumber);
    }

    [Fact]
    public void ParseLine_ErrorKeyword_SetsErrorLevel()
    {
        var entry = LogMonitorService.ParseLine("Error: something went wrong", 5);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
        Assert.Equal(5, entry.LineNumber);
        Assert.Null(entry.SourceFile);
    }

    [Fact]
    public void ParseLine_FailedKeyword_SetsErrorLevel()
    {
        var entry = LogMonitorService.ParseLine("Failed to load resource", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
    }

    [Fact]
    public void ParseLine_ExceptionKeyword_SetsErrorLevel()
    {
        var entry = LogMonitorService.ParseLine("Exception in thread main", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
    }

    [Fact]
    public void ParseLine_TracebackKeyword_SetsErrorLevel()
    {
        var entry = LogMonitorService.ParseLine("Traceback (most recent call last):", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
    }

    [Fact]
    public void ParseLine_WarningKeyword_SetsWarningLevel()
    {
        var entry = LogMonitorService.ParseLine("Warning: deprecated function used", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Warning, entry!.Level);
    }

    [Fact]
    public void ParseLine_WarnKeyword_SetsWarningLevel()
    {
        var entry = LogMonitorService.ParseLine("warn: low memory", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Warning, entry!.Level);
    }

    [Fact]
    public void ParseLine_BindingOfIsaacLine_ReturnsInfoEntry()
    {
        var entry = LogMonitorService.ParseLine("Binding of Isaac: Repentance v1.7.9c", 1);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Info, entry!.Level);
    }

    [Fact]
    public void ParseLine_WhitespaceOnly_ReturnsNull()
    {
        var entry = LogMonitorService.ParseLine("   ", 1);
        Assert.Null(entry);
    }

    [Fact]
    public void ParseLine_EmptyString_ReturnsNull()
    {
        var entry = LogMonitorService.ParseLine("", 1);
        Assert.Null(entry);
    }

    [Fact]
    public void ParseLine_UnrelatedLine_ReturnsNull()
    {
        var entry = LogMonitorService.ParseLine("just some random log line", 1);
        Assert.Null(entry);
    }

    [Fact]
    public void ParseLine_LuaErrorWithFullPath_ParsesCorrectly()
    {
        var entry = LogMonitorService.ParseLine("scripts/utils/helper.lua:123: bad argument #1", 10);

        Assert.NotNull(entry);
        Assert.Equal(LogEntry.EntryLevel.Error, entry!.Level);
        Assert.Equal("scripts/utils/helper.lua", entry.SourceFile);
        Assert.Equal(123, entry.SourceLine);
    }

    [Fact]
    public void LogEntry_LevelLabel_ReturnsCorrectLabel()
    {
        Assert.Equal("ERR", new LogEntry { Level = LogEntry.EntryLevel.Error }.LevelLabel);
        Assert.Equal("WRN", new LogEntry { Level = LogEntry.EntryLevel.Warning }.LevelLabel);
        Assert.Equal("INF", new LogEntry { Level = LogEntry.EntryLevel.Info }.LevelLabel);
    }

    [Fact]
    public void Start_NonExistentFile_ReturnsFalse()
    {
        var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
        var result = svc.Start("/nonexistent/path/log.txt");

        Assert.False(result);
        Assert.False(svc.IsMonitoring);
        Assert.Contains("not found", svc.StatusText);
    }

    [Fact]
    public void Start_NullPathAndNoDefaultLog_ReturnsFalseOrTrueIfExists()
    {
        // GetDefaultLogPath returns null if the file doesn't exist.
        // On the developer's machine, the Isaac log might actually exist.
        var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
        var result = svc.Start(null);

        if (result)
        {
            Assert.True(svc.IsMonitoring);
            svc.Dispose();
        }
        else
        {
            Assert.False(svc.IsMonitoring);
        }
    }

    [Fact]
    public void Stop_WhenNotMonitoring_SetsStoppedStatus()
    {
        var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
        svc.Stop();

        Assert.False(svc.IsMonitoring);
        Assert.Equal("Stopped", svc.StatusText);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
        svc.Entries.Add(new LogEntry { Level = LogEntry.EntryLevel.Error, Line = "test" });
        svc.Entries.Add(new LogEntry { Level = LogEntry.EntryLevel.Warning, Line = "test2" });

        svc.Clear();

        Assert.Empty(svc.Entries);
    }

    [Fact]
    public void Start_ValidLogFile_ReadsExistingContent()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"isaac_log_{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFile, """
            Binding of Isaac: Repentance v1.7.9c
            main.lua:10: attempt to call nil value
            Warning: deprecated function
            """);
        try
        {
            var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
            var result = svc.Start(tempFile);

            Assert.True(result);
            Assert.True(svc.IsMonitoring);
            Assert.NotEmpty(svc.Entries);
            Assert.Contains(svc.Entries, e => e.Level == LogEntry.EntryLevel.Error && e.SourceLine == 10);

            svc.Dispose();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Dispose_AfterStart_StopsMonitoring()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"isaac_log_{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFile, "Binding of Isaac: Repentance\n");
        try
        {
            var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
            svc.Start(tempFile);
            Assert.True(svc.IsMonitoring);

            svc.Dispose();

            Assert.False(svc.IsMonitoring);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotThrow()
    {
        var svc = new LogMonitorService(Mock.Of<ILogger<LogMonitorService>>());
        svc.Dispose();
        // Second dispose should be a no-op
        svc.Dispose();
    }
}
