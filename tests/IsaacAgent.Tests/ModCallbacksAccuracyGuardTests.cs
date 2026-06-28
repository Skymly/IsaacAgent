using System.Reflection;
using System.Text.RegularExpressions;
using IsaacAgent.Core.Knowledge;
using Xunit;

namespace IsaacAgent.Tests;

/// <summary>
/// Guard tests that verify the C# callback ID mappings in
/// <see cref="ModCallbacks"/> match the official documentation embedded
/// in IsaacAgent.Rag/Resources/docs. These tests prevent recurrence of
/// KB-1, a systematic ID mapping error where vanilla callback IDs were
/// wrong from ID 4 onward.
/// </summary>
public class ModCallbacksAccuracyGuardTests
{
    private static readonly Regex TableRowPattern = new(
        @"\|(\d+)\s*\|(MC_\w+)\s*\{:",
        RegexOptions.Compiled);

    /// <summary>
    /// Loads an embedded markdown resource from the IsaacAgent.Rag assembly.
    /// </summary>
    private static string LoadEmbeddedDoc(string resourcePath)
    {
        // Force-load the Rag assembly if it hasn't been loaded yet (e.g.
        // when running a filtered test that doesn't touch Rag types).
        var ragAsm = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "IsaacAgent.Rag")
            ?? Assembly.Load(new AssemblyName("IsaacAgent.Rag"));

        using var stream = ragAsm.GetManifestResourceStream(resourcePath)
            ?? throw new FileNotFoundException($"Embedded resource not found: {resourcePath}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Parses all (id, name) pairs from a ModCallbacks markdown table.
    /// Works for both vanilla (with DLC badge column) and REPENTOGON table
    /// formats — the unified pattern matches "|{id} |{name} {: .copyable }".
    /// Returns a set because REPENTOGON reuses some IDs across sections
    /// (e.g. 1064 for both MC_USE_PILL and MC_PRE_USE_CARD).
    /// </summary>
    private static HashSet<(int Id, string Name)> ParseCallbackTable(string markdown)
    {
        var result = new HashSet<(int, string)>();
        foreach (Match match in TableRowPattern.Matches(markdown))
        {
            var id = int.Parse(match.Groups[1].Value);
            var name = match.Groups[2].Value;
            result.Add((id, name));
        }
        return result;
    }

    private static readonly string VanillaMarkdown = LoadEmbeddedDoc(
        "IsaacAgent.Rag.Resources.docs.vanilla.enums.ModCallbacks.md");

    private static readonly string RepentogonMarkdown = LoadEmbeddedDoc(
        "IsaacAgent.Rag.Resources.docs.repentogon.enums.ModCallbacks.md");

    // ── Vanilla callbacks: bidirectional exact match ──────────────

    [Fact]
    public void Vanilla_Callbacks_MatchMarkdownExactly()
    {
        var markdownEntries = ParseCallbackTable(VanillaMarkdown);

        // The vanilla doc table has exactly 74 callback rows (IDs 0-73).
        Assert.Equal(74, markdownEntries.Count);

        // Build the C# set for set comparison.
        var csharpEntries = ModCallbacks.Callbacks
            .Select(kv => (kv.Value.Id, kv.Key))
            .ToHashSet();

        Assert.Equal(markdownEntries, csharpEntries);
    }

    // ── RepentogonModifiedIds: must be vanilla callback overrides ──

    [Fact]
    public void RepentogonModifiedIds_AllExistInVanillaCallbacks()
    {
        // RepentogonModifiedIds maps vanilla callback names to their
        // REPENTOGON override IDs. Every name must exist in the vanilla
        // Callbacks dictionary — otherwise it's a REPENTOGON-exclusive
        // callback that belongs in RepentogonCallbacks instead.
        foreach (var (name, id) in ModCallbacks.RepentogonModifiedIds)
        {
            Assert.True(ModCallbacks.Callbacks.ContainsKey(name),
                $"RepentogonModifiedIds contains '{name}' (ID {id}) but it is not a vanilla callback. " +
                "Move it to RepentogonCallbacks.");
        }
    }

    [Fact]
    public void RepentogonModifiedIds_MatchMarkdownIds()
    {
        var markdownEntries = ParseCallbackTable(RepentogonMarkdown);

        foreach (var (name, id) in ModCallbacks.RepentogonModifiedIds)
        {
            Assert.Contains((id, name), markdownEntries);
        }
    }

    // ── RepentogonCallbacks: C# entries must exist in markdown ────

    [Fact]
    public void RepentogonCallbacks_MatchMarkdownIds()
    {
        var markdownEntries = ParseCallbackTable(RepentogonMarkdown);

        // One-directional: every C# entry must appear in the markdown.
        // The markdown may contain more callbacks than the C# dictionary
        // (the C# dictionary is a curated subset), but every curated entry
        // must have the correct ID.
        foreach (var (name, info) in ModCallbacks.RepentogonCallbacks)
        {
            Assert.Contains((info.Id, name), markdownEntries);
        }
    }

    // ── No name overlap between the two REPENTOGON dictionaries ────
    // (ID overlap is allowed — REPENTOGON reuses IDs across sections.)

    [Fact]
    public void Repentogon_NoNameOverlapBetweenDictionaries()
    {
        var modifiedNames = ModCallbacks.RepentogonModifiedIds.Keys;
        var newNames = ModCallbacks.RepentogonCallbacks.Keys;
        var overlap = modifiedNames.Intersect(newNames).ToList();
        Assert.Empty(overlap);
    }
}
