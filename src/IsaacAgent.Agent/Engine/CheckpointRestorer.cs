using System.Text;
using IsaacAgent.Core.PathSafety;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Records tip hashes after successful Tracked writes and applies Before-images
/// during Restore under the configured Hand-edit conflict mode.
/// </summary>
internal sealed class CheckpointRestorer
{
    public const string ReasonMissingBeforeImage = "missing-before-image";
    public const string ReasonHandEdit = "hand-edit";
    public const string ReasonUnreadable = "unreadable";

    private readonly ILogger _logger;
    private readonly TrackedWriteTipStore _tips;
    private readonly Func<string?> _getProjectDir;

    public CheckpointRestorer(
        ILogger logger,
        TrackedWriteTipStore tips,
        Func<string?> getProjectDir)
    {
        _logger = logger;
        _tips = tips;
        _getProjectDir = getProjectDir;
    }

    public async Task RecordTipsAfterTrackedWriteAsync(
        string toolName,
        string arguments,
        string toolResult,
        CancellationToken ct = default)
    {
        if (!BeforeImageCapturer.IsTrackedWrite(toolName))
            return;
        if (toolResult.StartsWith("Error", StringComparison.Ordinal))
            return;

        var projectDir = _getProjectDir();
        if (string.IsNullOrEmpty(projectDir))
            return;

        foreach (var relativePath in BeforeImageCapturer.DeriveRelativePaths(toolName, arguments))
        {
            var (fullPath, isSafe) = ProjectPathSafety.Resolve(projectDir, relativePath);
            if (!isSafe || !File.Exists(fullPath))
                continue;

            try
            {
                var bytes = await File.ReadAllBytesAsync(fullPath, ct);
                var canonical = CheckpointRelativePaths.ToRelativeKey(projectDir, fullPath);
                _tips.SetTip(canonical, TrackedWriteTipStore.HashBytes(bytes));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Failed to record tip hash for {RelativePath}", relativePath);
            }
        }
    }

    public async Task ApplyAsync(
        Checkpoint checkpoint,
        HandEditConflictMode conflictMode,
        List<string> restored,
        List<RestoreSkippedPath> skipped,
        CancellationToken ct)
    {
        try
        {
            var projectDir = _getProjectDir();
            if (string.IsNullOrEmpty(projectDir))
            {
                foreach (var path in checkpoint.TouchedPathsWithoutBeforeImage)
                    skipped.Add(new RestoreSkippedPath(path, ReasonMissingBeforeImage));
                foreach (var path in checkpoint.BeforeImages.Keys)
                    skipped.Add(new RestoreSkippedPath(path, ReasonMissingBeforeImage));
                return;
            }

            foreach (var path in checkpoint.TouchedPathsWithoutBeforeImage)
                skipped.Add(new RestoreSkippedPath(path, ReasonMissingBeforeImage));

            foreach (var (relativePath, image) in checkpoint.BeforeImages)
            {
                ct.ThrowIfCancellationRequested();
                await ApplyOneAsync(projectDir, relativePath, image, conflictMode, restored, skipped, ct);
            }
        }
        finally
        {
            // Restore leaves the session "before" later Tracked writes; tip hashes
            // from those writes must not survive even if apply exits early.
            _tips.Clear();
        }
    }

    private async Task ApplyOneAsync(
        string projectDir,
        string relativePath,
        BeforeImage image,
        HandEditConflictMode conflictMode,
        List<string> restored,
        List<RestoreSkippedPath> skipped,
        CancellationToken ct)
    {
        var (fullPath, isSafe) = ProjectPathSafety.Resolve(projectDir, relativePath);
        if (!isSafe)
        {
            skipped.Add(new RestoreSkippedPath(relativePath, ReasonMissingBeforeImage));
            return;
        }

        if (conflictMode == HandEditConflictMode.Skip)
        {
            var decision = await EvaluateHandEditAsync(fullPath, relativePath, ct);
            if (decision == HandEditDecision.HandEdit)
            {
                skipped.Add(new RestoreSkippedPath(relativePath, ReasonHandEdit));
                return;
            }
            if (decision == HandEditDecision.Unreadable)
            {
                skipped.Add(new RestoreSkippedPath(relativePath, ReasonUnreadable));
                return;
            }
        }

        try
        {
            if (image.IsTombstone)
            {
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            else
            {
                var dir = Path.GetDirectoryName(fullPath);
                if (dir is not null)
                    Directory.CreateDirectory(dir);
                await File.WriteAllTextAsync(
                    fullPath,
                    image.Content!,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    ct);
            }

            restored.Add(relativePath);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to apply Before-image for {RelativePath}", relativePath);
            skipped.Add(new RestoreSkippedPath(relativePath, ReasonUnreadable));
        }
    }

    private async Task<HandEditDecision> EvaluateHandEditAsync(
        string fullPath,
        string relativePath,
        CancellationToken ct)
    {
        if (!File.Exists(fullPath))
            return HandEditDecision.HandEdit;

        // Without a tip we cannot compare; skip mode must not overwrite blindly.
        if (!_tips.TryGetTip(relativePath, out var tipHash) || tipHash is null)
            return HandEditDecision.Unreadable;

        try
        {
            var bytes = await File.ReadAllBytesAsync(fullPath, ct);
            var diskHash = TrackedWriteTipStore.HashBytes(bytes);
            return string.Equals(diskHash, tipHash, StringComparison.Ordinal)
                ? HandEditDecision.NoConflict
                : HandEditDecision.HandEdit;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Unreadable during Hand-edit compare for {RelativePath}", relativePath);
            return HandEditDecision.Unreadable;
        }
    }

    private enum HandEditDecision
    {
        NoConflict,
        HandEdit,
        Unreadable
    }
}
