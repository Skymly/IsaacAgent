namespace IsaacAgent.Core.Models;

public sealed class Diagnostic
{
    public required DiagnosticSeverity Severity { get; init; }
    public required string Message { get; init; }
    public required string FilePath { get; init; }
    public int Line { get; init; }
    public int Column { get; init; }
    public string? Code { get; init; }
    public string? Suggestion { get; init; }
}

public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Hint
}

