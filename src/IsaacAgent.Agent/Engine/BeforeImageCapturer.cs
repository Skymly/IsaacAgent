using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Derives Tracked-write target paths and lazily records Before-images on
/// Checkpoints before tool mutation.
/// </summary>
internal sealed class BeforeImageCapturer
{
    public const int MaxContentBytes = 256 * 1024;

    private static readonly HashSet<string> TrackedWriteTools = new(StringComparer.Ordinal)
    {
        "write_file",
        "diff_apply",
        "batch_edit",
        "scaffold_mod"
    };

    private readonly ILogger _logger;
    private readonly Func<IReadOnlyList<Checkpoint>> _getCheckpoints;
    private readonly Func<string?> _getProjectDir;

    public BeforeImageCapturer(
        ILogger logger,
        Func<IReadOnlyList<Checkpoint>> getCheckpoints,
        Func<string?> getProjectDir)
    {
        _logger = logger;
        _getCheckpoints = getCheckpoints;
        _getProjectDir = getProjectDir;
    }

    public static bool IsTrackedWrite(string toolName) =>
        TrackedWriteTools.Contains(toolName);

    public async Task MaybeCaptureAsync(string toolName, string arguments, CancellationToken ct = default)
    {
        if (!IsTrackedWrite(toolName))
            return;

        var projectDir = _getProjectDir();
        if (string.IsNullOrEmpty(projectDir))
            return;

        var checkpoints = _getCheckpoints();
        if (checkpoints.Count == 0)
            return;

        var relativePaths = DeriveRelativePaths(toolName, arguments);
        foreach (var relativePath in relativePaths)
            await CaptureForPathAsync(projectDir, relativePath, checkpoints, ct);
    }

    private async Task CaptureForPathAsync(
        string projectDir,
        string relativePath,
        IReadOnlyList<Checkpoint> checkpoints,
        CancellationToken ct)
    {
        var needing = checkpoints.Where(cp => !cp.HasTouchedPath(relativePath)).ToList();
        if (needing.Count == 0)
            return;

        var (fullPath, isSafe) = ProjectPathSafety.Resolve(projectDir, relativePath);
        if (!isSafe)
        {
            MarkTouched(needing, relativePath);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                relativePath,
                "unsafe-path");
            return;
        }

        var canonical = ProjectPathSafety.ToRelativeKey(projectDir, fullPath);
        // Re-key needing against canonical (may differ in separators/casing form)
        needing = checkpoints.Where(cp => !cp.HasTouchedPath(canonical)).ToList();
        if (needing.Count == 0)
            return;

        if (!File.Exists(fullPath))
        {
            var tombstone = new BeforeImage(canonical, isTombstone: true, content: null);
            foreach (var cp in needing)
                cp.TryRecordBeforeImage(tombstone);
            _logger.LogInformation(
                "Before-image captured for {RelativePath}: tombstone (path did not exist)",
                canonical);
            return;
        }

        long length;
        try
        {
            length = new FileInfo(fullPath).Length;
        }
        catch (Exception ex)
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                $"unreadable ({ex.GetType().Name})");
            return;
        }

        if (length > MaxContentBytes)
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                "over-limit");
            return;
        }

        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(fullPath, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                $"unreadable ({ex.GetType().Name})");
            return;
        }

        if (bytes.Length > MaxContentBytes)
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                "over-limit");
            return;
        }

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                "binary");
            return;
        }

        if (text.Contains('\0'))
        {
            MarkTouched(needing, canonical);
            _logger.LogInformation(
                "Before-image skipped for {RelativePath}: {Reason}",
                canonical,
                "binary");
            return;
        }

        var image = new BeforeImage(canonical, isTombstone: false, content: text);
        foreach (var cp in needing)
            cp.TryRecordBeforeImage(image);
        _logger.LogInformation(
            "Before-image captured for {RelativePath}: {ByteCount} bytes",
            canonical,
            bytes.Length);
    }

    private static void MarkTouched(IEnumerable<Checkpoint> checkpoints, string relativePath)
    {
        foreach (var cp in checkpoints)
            cp.TryMarkPathTouched(relativePath);
    }

    internal static IReadOnlyList<string> DeriveRelativePaths(string toolName, string arguments)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
            var root = doc.RootElement;
            return toolName switch
            {
                "write_file" or "diff_apply" => DeriveSinglePath(root),
                "batch_edit" => DeriveBatchEditPaths(root),
                "scaffold_mod" => DeriveScaffoldPaths(root),
                _ => []
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static IReadOnlyList<string> DeriveSinglePath(JsonElement root)
    {
        if (!root.TryGetProperty("path", out var pathEl))
            return [];
        var path = pathEl.GetString();
        return string.IsNullOrWhiteSpace(path) ? [] : [path];
    }

    private static IReadOnlyList<string> DeriveBatchEditPaths(JsonElement root)
    {
        if (!root.TryGetProperty("edits", out var edits) || edits.ValueKind != JsonValueKind.Array)
            return [];

        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var edit in edits.EnumerateArray())
        {
            if (!edit.TryGetProperty("path", out var pathEl))
                continue;
            var path = pathEl.GetString();
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (seen.Add(path))
                paths.Add(path);
        }
        return paths;
    }

    private static IReadOnlyList<string> DeriveScaffoldPaths(JsonElement root)
    {
        var paths = new List<string> { "main.lua", "metadata.xml" };
        if (root.TryGetProperty("include_items", out var items) && items.ValueKind == JsonValueKind.True)
            paths.Add("items.xml");
        if (root.TryGetProperty("include_trinkets", out var trinkets) && trinkets.ValueKind == JsonValueKind.True)
            paths.Add("trinkets.xml");
        if (root.TryGetProperty("include_entity", out var entity) && entity.ValueKind == JsonValueKind.True)
            paths.Add("entities2.xml");
        return paths;
    }
}
