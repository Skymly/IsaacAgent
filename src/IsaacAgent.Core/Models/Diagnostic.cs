namespace IsaacAgent.Core.Models;

/// <summary>
/// Represents a single diagnostic message (error, warning, etc.) for a source file.
/// </summary>
public sealed class Diagnostic
{
    /// <summary>Severity level of the diagnostic.</summary>
    public required DiagnosticSeverity Severity { get; init; }

    /// <summary>Human-readable diagnostic message.</summary>
    public required string Message { get; init; }

    /// <summary>Path of the file the diagnostic applies to.</summary>
    public required string FilePath { get; init; }

    /// <summary>1-based line number, or 0 if not applicable.</summary>
    public int Line { get; init; }

    /// <summary>1-based column number, or 0 if not applicable.</summary>
    public int Column { get; init; }

    /// <summary>Diagnostic code identifier, if available.</summary>
    public string? Code { get; init; }

    /// <summary>Suggested fix for the diagnostic, if available.</summary>
    public string? Suggestion { get; init; }
}

/// <summary>
/// Severity levels for a <see cref="Diagnostic"/>.
/// </summary>
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint
}

