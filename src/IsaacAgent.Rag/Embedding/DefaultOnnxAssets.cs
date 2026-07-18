using System.Reflection;
using System.Security.Cryptography;

namespace IsaacAgent.Rag.Embedding;

/// <summary>
/// Resolves the bundled all-MiniLM-L6-v2 ONNX model and WordPiece vocab.
/// Empty / whitespace configured paths fall back to these defaults so ONNX
/// works with zero user setup (ADR-002).
/// <para>
/// Resolution order:
/// 1. Side-by-side files under <c>{BaseDirectory}/onnx/</c> (local builds / folder publish).
/// 2. Extracted copies under <c>%APPDATA%/IsaacAgent/onnx/</c> from embedded resources
///    (self-contained single-file releases that only ship the exe).
/// </para>
/// </summary>
public static class DefaultOnnxAssets
{
    public const string ModelFileName = "model.onnx";
    public const string VocabFileName = "vocab.txt";
    public const string RelativeDirectory = "onnx";

    internal const string EmbeddedModelName = "IsaacAgent.Rag.Onnx.model.onnx";
    internal const string EmbeddedVocabName = "IsaacAgent.Rag.Onnx.vocab.txt";

    private static readonly object ExtractLock = new();

    public static string BundledModelPath => Path.Combine(GetAssetsDirectory(), ModelFileName);

    public static string BundledVocabPath => Path.Combine(GetAssetsDirectory(), VocabFileName);

    public static string ResolveModelPath(string? configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) ? BundledModelPath : configuredPath;

    public static string ResolveVocabPath(string? configuredPath) =>
        string.IsNullOrWhiteSpace(configuredPath) ? BundledVocabPath : configuredPath;

    /// <summary>
    /// Returns a directory that contains both model and vocab, extracting from
    /// embedded resources into AppData when side-by-side files are absent.
    /// </summary>
    public static string GetAssetsDirectory()
    {
        var sideBySide = Path.Combine(AppContext.BaseDirectory, RelativeDirectory);
        if (AssetsPresent(sideBySide))
            return sideBySide;

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IsaacAgent",
            RelativeDirectory);

        EnsureExtracted(appData);
        return appData;
    }

    private static bool AssetsPresent(string directory) =>
        File.Exists(Path.Combine(directory, ModelFileName))
        && File.Exists(Path.Combine(directory, VocabFileName));

    private static void EnsureExtracted(string directory)
    {
        lock (ExtractLock)
        {
            if (AssetsPresent(directory))
                return;

            Directory.CreateDirectory(directory);
            ExtractResource(EmbeddedModelName, Path.Combine(directory, ModelFileName));
            ExtractResource(EmbeddedVocabName, Path.Combine(directory, VocabFileName));
        }
    }

    private static void ExtractResource(string resourceName, string destinationPath)
    {
        var assembly = typeof(DefaultOnnxAssets).Assembly;
        using var source = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(
                $"Embedded ONNX asset '{resourceName}' is missing from {assembly.GetName().Name}. Rebuild IsaacAgent.Rag to restore bundled assets.",
                resourceName);

        // Write to a temp file then replace, so a crashed extract cannot leave a truncated model.
        var tempPath = destinationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var target = File.Create(tempPath))
                source.CopyTo(target);

            File.Move(tempPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort */ }
            }
        }
    }

    /// <summary>SHA-256 of an embedded resource stream (for tests / diagnostics).</summary>
    internal static string HashEmbeddedResource(string resourceName)
    {
        var assembly = typeof(DefaultOnnxAssets).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException(resourceName);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }
}
