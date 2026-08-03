namespace IsaacAgent.UiTests;

internal static class MinimalModFixture
{
    public static string ResolveDirectory()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "MinimalMod");
        if (!Directory.Exists(dir))
        {
            throw new DirectoryNotFoundException(
                "MinimalMod fixture missing from test output. Expected: " + dir);
        }

        if (!File.Exists(Path.Combine(dir, "main.lua")) ||
            !File.Exists(Path.Combine(dir, "metadata.xml")))
        {
            throw new FileNotFoundException(
                "MinimalMod fixture is incomplete (need main.lua and metadata.xml): " + dir);
        }

        return Path.GetFullPath(dir);
    }
}
