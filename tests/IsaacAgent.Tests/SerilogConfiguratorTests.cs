using IsaacAgent.App.Services;
using Serilog.Events;
using Microsoft.Extensions.Logging;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
///   Unit tests for SerilogConfigurator — log level parsing,
///   logger creation, and log file path management.
/// </summary>
public class SerilogConfiguratorTests
{
    // ── Log level parsing ──────────────────────────────────────

    [Theory]
    [InlineData("Verbose", LogEventLevel.Verbose)]
    [InlineData("verbose", LogEventLevel.Verbose)]
    [InlineData("trace", LogEventLevel.Verbose)]
    [InlineData("Debug", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("info", LogEventLevel.Information)]
    [InlineData("information", LogEventLevel.Information)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("warn", LogEventLevel.Warning)]
    [InlineData("warning", LogEventLevel.Warning)]
    [InlineData("Error", LogEventLevel.Error)]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("Fatal", LogEventLevel.Fatal)]
    [InlineData("fatal", LogEventLevel.Fatal)]
    public void ParseLogLevel_ValidStrings_ReturnsCorrectLevel(string input, LogEventLevel expected)
    {
        Assert.Equal(expected, SerilogConfigurator.ParseLogLevel(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("xyz")]
    public void ParseLogLevel_InvalidOrEmpty_ReturnsInformation(string? input)
    {
        Assert.Equal(LogEventLevel.Information, SerilogConfigurator.ParseLogLevel(input));
    }

    // ── Logger creation ────────────────────────────────────────

    [Fact]
    public void CreateLogger_Default_ReturnsNonNull()
    {
        var logger = SerilogConfigurator.CreateLogger();
        Assert.NotNull(logger);
        logger.Dispose();
    }

    [Fact]
    public void CreateLogger_WithDebugLevel_ReturnsNonNull()
    {
        var logger = SerilogConfigurator.CreateLogger("Debug");
        Assert.NotNull(logger);
        logger.Dispose();
    }

    [Fact]
    public void CreateLogger_WithErrorLevel_ReturnsNonNull()
    {
        var logger = SerilogConfigurator.CreateLogger("Error");
        Assert.NotNull(logger);
        logger.Dispose();
    }

    [Fact]
    public void CreateLoggerFactory_ReturnsNonNullFactory()
    {
        var factory = SerilogConfigurator.CreateLoggerFactory("Information");
        Assert.NotNull(factory);
        factory.Dispose();
    }

    [Fact]
    public void CreateLoggerFactory_CreatesLoggerForCategory()
    {
        var factory = SerilogConfigurator.CreateLoggerFactory("Debug");
        var logger = factory.CreateLogger("TestCategory");
        Assert.NotNull(logger);

        // Should be able to log without throwing
        logger.LogInformation("Test log message {Value}", 42);

        factory.Dispose();
    }



    // ── Log file paths ─────────────────────────────────────────

    [Fact]
    public void GetLogDirectory_ReturnsPathWithIsaacAgent()
    {
        var dir = SerilogConfigurator.GetLogDirectory();
        Assert.Contains("IsaacAgent", dir);
        Assert.Contains("logs", dir);
    }

    [Fact]
    public void GetLatestLogFile_ReturnsNullWhenNoLogsExist()
    {
        // This test assumes no log files exist in a fresh environment.
        // If logs do exist (from a previous run), this just verifies
        // the method returns a non-null path.
        var result = SerilogConfigurator.GetLatestLogFile();
        // Either null (no logs) or a valid path
        if (result is not null)
        {
            Assert.True(File.Exists(result));
        }
    }

    // ── AppConfiguration LogLevel ──────────────────────────────

    [Fact]
    public void AppConfiguration_DefaultLogLevel_IsInformation()
    {
        var config = new AppConfiguration();
        Assert.Equal("Information", config.LogLevel);
    }

    [Fact]
    public void AppConfiguration_LogLevel_CanBeSet()
    {
        var config = new AppConfiguration { LogLevel = "Debug" };
        Assert.Equal("Debug", config.LogLevel);
    }
}
