using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
///   Configures and manages the Serilog logging pipeline.
///   Provides file logging with daily rotation and JSON formatting,
///   plus console output. Bridges to Microsoft.Extensions.Logging.
/// </summary>
public static class SerilogConfigurator
{
    /// <summary>
    ///   Log file path pattern. Files are stored in
    ///   %APPDATA%/IsaacAgent/logs/ with daily rolling.
    /// </summary>
    private static string GetLogPathPattern() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IsaacAgent", "logs", "app-.log");

    /// <summary>
    ///   Create and configure the Serilog logger.
    /// </summary>
    /// <param name="minimumLevel">
    ///   Minimum log level as string: "Verbose", "Debug", "Information",
    ///   "Warning", "Error", or "Fatal". Defaults to "Information".
    /// </param>
    public static Logger CreateLogger(string? minimumLevel = null)
    {
        var level = ParseLogLevel(minimumLevel);

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            // Suppress noisy framework logs below Warning
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .MinimumLevel.Override("Avalonia", LogEventLevel.Warning)

            // Console sink: human-readable, single-line with timestamp
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information)

            // File sink: structured JSON (compact), daily rolling, 7-day retention
            .WriteTo.File(
                path: GetLogPathPattern(),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                formatter: new Serilog.Formatting.Compact.CompactJsonFormatter(),
                shared: true);

        return loggerConfig.CreateLogger();
    }

    /// <summary>
    ///   Create a Microsoft.Extensions.Logging.ILoggerFactory
    ///   backed by Serilog.
    /// </summary>
    public static ILoggerFactory CreateLoggerFactory(string? minimumLevel = null)
    {
        var serilogLogger = CreateLogger(minimumLevel);
        Log.Logger = serilogLogger;
        return new SerilogLoggerFactory(serilogLogger, dispose: true);
    }

    /// <summary>
    ///   Parse a log level string into a Serilog LogEventLevel.
    /// </summary>
    public static LogEventLevel ParseLogLevel(string? level)
    {
        return level?.ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }

    /// <summary>
    ///   Get the directory where log files are stored.
    /// </summary>
    public static string GetLogDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "IsaacAgent", "logs");

    /// <summary>
    ///   Get the most recent log file path, if any exists.
    /// </summary>
    public static string? GetLatestLogFile()
    {
        var dir = GetLogDirectory();
        if (!Directory.Exists(dir)) return null;
        return Directory.GetFiles(dir, "app-*.log")
            .OrderByDescending(f => f)
            .FirstOrDefault();
    }
}
