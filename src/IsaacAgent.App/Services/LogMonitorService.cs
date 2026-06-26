using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.App.Services;

/// <summary>
/// Represents a single log entry parsed from Isaac's log.txt.
/// </summary>
public sealed class LogEntry
{
    public enum EntryLevel { Info, Warning, Error }

    public EntryLevel Level { get; init; }
    public string Line { get; init; } = "";
    public int LineNumber { get; init; }
    public string? SourceFile { get; init; }
    public int? SourceLine { get; init; }

    public string LevelLabel => Level switch
    {
        EntryLevel.Error => "ERR",
        EntryLevel.Warning => "WRN",
        _ => "INF",
    };
}

/// <summary>
/// Monitors Isaac's log.txt file in real-time, parsing new lines for
/// errors and warnings. Uses FileSystemWatcher with polling fallback.
/// </summary>
public sealed partial class LogMonitorService : ObservableObject, IDisposable
{
    private static readonly Regex LuaErrorPattern = new(
        @"^(.+\.lua):(\d+):\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex WarningPattern = new(
        @"\b(warning|warn)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ErrorPattern = new(
        @"\b(error|err|failed|exception|traceback)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<LogMonitorService>? _logger;
    private FileSystemWatcher? _watcher;
    private string? _logPath;
    private long _lastPosition;
    private bool _disposed;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _statusText = "Not monitoring";

    public ObservableCollection<LogEntry> Entries { get; } = [];

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _showWarnings = true;

    [ObservableProperty]
    private bool _showInfo;

    public LogMonitorService(ILogger<LogMonitorService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get the default Isaac log.txt path.
    /// </summary>
    public static string? GetDefaultLogPath()
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(docs, "My Games", "Binding of Isaac Repentance", "log.txt");
        return File.Exists(path) ? path : null;
    }

    /// <summary>
    /// Start monitoring the given log file (or the default Isaac log).
    /// </summary>
    public bool Start(string? logPath = null)
    {
        _logPath = logPath ?? GetDefaultLogPath();
        if (_logPath is null || !File.Exists(_logPath))
        {
            StatusText = "log.txt not found. Start the game or set the path in Settings.";
            IsMonitoring = false;
            return false;
        }

        try
        {
            // Read existing content first
            _lastPosition = 0;
            ReadNewLines();

            var dir = Path.GetDirectoryName(_logPath);
            var fileName = Path.GetFileName(_logPath);
            if (dir is null || fileName is null) return false;

            _watcher = new FileSystemWatcher(dir, fileName)
            {
                NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            _watcher.Changed += OnFileChanged;

            IsMonitoring = true;
            StatusText = $"Monitoring: {_logPath}";
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to start log monitor");
            StatusText = $"Error: {ex.Message}";
            IsMonitoring = false;
            return false;
        }
    }

    /// <summary>
    /// Stop monitoring and clear entries.
    /// </summary>
    public void Stop()
    {
        if (_watcher is not null)
        {
            _watcher.Changed -= OnFileChanged;
            _watcher.Dispose();
            _watcher = null;
        }
        IsMonitoring = false;
        StatusText = "Stopped";
    }

    /// <summary>
    /// Clear all entries.
    /// </summary>
    public void Clear()
    {
        Entries.Clear();
    }

    private void OnFileChanged(object? sender, FileSystemEventArgs e)
    {
        // FileSystemWatcher fires on a background thread; dispatch to UI.
        Dispatcher.UIThread.Post(ReadNewLines, DispatcherPriority.Background);
    }

    private void ReadNewLines()
    {
        if (_logPath is null || !File.Exists(_logPath)) return;

        try
        {
            using var fs = new FileStream(_logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length <= _lastPosition)
            {
                // File was truncated (new game session) — read from start
                _lastPosition = 0;
                Entries.Clear();
            }

            fs.Seek(_lastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            string? line;
            var lineNumber = 0;
            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                var entry = ParseLine(line, lineNumber);
                if (entry is not null)
                    Entries.Add(entry);
            }
            _lastPosition = fs.Position;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read log lines");
        }
    }

    private static LogEntry? ParseLine(string line, int lineNumber)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;

        var level = LogEntry.EntryLevel.Info;
        var match = LuaErrorPattern.Match(line);
        string? sourceFile = null;
        int? sourceLine = null;

        if (match.Success)
        {
            level = LogEntry.EntryLevel.Error;
            sourceFile = match.Groups[1].Value;
            sourceLine = int.Parse(match.Groups[2].Value);
        }
        else if (ErrorPattern.IsMatch(line))
        {
            level = LogEntry.EntryLevel.Error;
        }
        else if (WarningPattern.IsMatch(line))
        {
            level = LogEntry.EntryLevel.Warning;
        }
        else
        {
            // Skip non-error/warning lines unless they look important
            if (!line.Contains("Binding of Isaac", StringComparison.OrdinalIgnoreCase))
                return null;
        }

        return new LogEntry
        {
            Level = level,
            Line = line.Trim(),
            LineNumber = lineNumber,
            SourceFile = sourceFile,
            SourceLine = sourceLine
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        _disposed = true;
    }
}
