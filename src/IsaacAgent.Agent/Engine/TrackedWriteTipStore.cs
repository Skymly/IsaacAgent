using System.Security.Cryptography;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Session-scoped tip hashes for Hand-edit detection: content hash left by the
/// last successful Tracked write to each path.
/// </summary>
internal sealed class TrackedWriteTipStore
{
    private readonly Dictionary<string, string> _tips =
        new(StringComparer.OrdinalIgnoreCase);

    public void Clear() => _tips.Clear();

    public void SetTip(string relativePath, string contentHash) =>
        _tips[relativePath] = contentHash;

    public bool TryGetTip(string relativePath, out string? contentHash) =>
        _tips.TryGetValue(relativePath, out contentHash);

    public static string HashBytes(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash);
    }
}
