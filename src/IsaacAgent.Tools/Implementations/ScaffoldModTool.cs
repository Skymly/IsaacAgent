using System.Security;
using System.Text.Json;
using IsaacAgent.Core.Models;
using IsaacAgent.Core.Services;

namespace IsaacAgent.Tools.Implementations;

public sealed class ScaffoldModTool : ITool
{
    public string Name => "scaffold_mod";
    public string Description => "Create a new Binding of Isaac: Repentance mod project structure with main.lua, metadata.xml, and optional files.";

    public ToolDefinition Definition => new()
    {
        Name = Name,
        Description = Description,
        Parameters = new ToolParameters
        {
            Properties = new()
            {
                ["name"] = new() { Type = "string", Description = "The mod name" },
                ["description"] = new() { Type = "string", Description = "Mod description" },
                ["author"] = new() { Type = "string", Description = "Author name" },
                ["include_items"] = new() { Type = "boolean", Description = "Include items.xml template (default: false)" },
                ["include_trinkets"] = new() { Type = "boolean", Description = "Include trinkets.xml template (default: false)" },
                ["include_entity"] = new() { Type = "boolean", Description = "Include entities2.xml template (default: false)" }
            },
            Required = ["name"]
        }
    };

    private readonly string _projectDir;

    public ScaffoldModTool(string projectDir) => _projectDir = Path.GetFullPath(projectDir);

    public async Task<string> ExecuteAsync(string arguments, CancellationToken ct = default)
    {
        var args = JsonDocument.Parse(arguments).RootElement;
        var name = args.GetProperty("name").GetString()!;
        var description = args.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
        var author = args.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "";
        var includeItems = args.TryGetProperty("include_items", out var i) && i.GetBoolean();
        var includeTrinkets = args.TryGetProperty("include_trinkets", out var t) && t.GetBoolean();
        var includeEntity = args.TryGetProperty("include_entity", out var e) && e.GetBoolean();

        // Escape user input for safe insertion into generated files.
        var luaName = EscapeLuaString(name);
        var xmlName = SecurityElement.Escape(name) ?? "";
        var xmlDesc = SecurityElement.Escape(description) ?? "";
        var xmlAuthor = SecurityElement.Escape(author) ?? "";

        var created = new List<string>();

        var mainLua = $"""
            local mod = RegisterMod("{luaName}", 1)

            function mod:onGameStart(isSave)
                -- Called when a new game starts or is continued
            end

            mod:AddCallback(ModCallbacks.MC_POST_GAME_STARTED, function(_, isSave)
                mod:onGameStart(isSave)
            end)

            -- Add your callbacks here

            print("{luaName} loaded!")
            """;
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "main.lua"), mainLua, ct);
        created.Add("main.lua");

        var metadata = $""""
            <?xml version="1.0" encoding="UTF-8"?>
            <metadata>
                <name>{xmlName}</name>
                <description>{xmlDesc}</description>
                <author>{xmlAuthor}</author>
                <version>1.0</version>
                <apiVersion>1</apiVersion>
            </metadata>
            """";
        await File.WriteAllTextAsync(Path.Combine(_projectDir, "metadata.xml"), metadata, ct);
        created.Add("metadata.xml");

        if (includeItems)
        {
            var itemsXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <items gfxroot="gfx/items/" lastgottenid="2000">
                    <active name="Custom Active Item"
                            description="A custom active item"
                            gfx="collectibles/001_sadonion.png"
                            maxcharges="2"
                            cache="damage"
                            tags="quest"
                            quality="3" />
                    <passive name="Custom Passive Item"
                             description="A custom passive item"
                             gfx="collectibles/002_innereye.png"
                             cache="damage"
                             quality="2" />
                </items>
                """;
            await File.WriteAllTextAsync(Path.Combine(_projectDir, "items.xml"), itemsXml, ct);
            created.Add("items.xml");
        }

        if (includeTrinkets)
        {
            var trinketsXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <trinkets gfxroot="gfx/items/" lastgottenid="200">
                    <trinket name="Custom Trinket"
                             description="A custom trinket"
                             gfx="trinkets/001_swallowedpenny.png"
                             quality="1" />
                </trinkets>
                """;
            await File.WriteAllTextAsync(Path.Combine(_projectDir, "trinkets.xml"), trinketsXml, ct);
            created.Add("trinkets.xml");
        }

        if (includeEntity)
        {
            var entitiesXml = """
                <?xml version="1.0" encoding="UTF-8"?>
                <entities anm2root="gfx/" lastid="950">
                    <entity name="Custom Entity"
                            ID="950"
                            Type="10"
                            Variant="1000"
                            SubType="0"
                            gfx="custom_entity.anm2"
                            friction="1"
                            shadow-size="12"
                            tags="enemy"
                            collisionmass="10"
                            collisionradius="12" />
                </entities>
                """;
            await File.WriteAllTextAsync(Path.Combine(_projectDir, "entities2.xml"), entitiesXml, ct);
            created.Add("entities2.xml");
        }

        var resourcesDir = Path.Combine(_projectDir, "resources");
        Directory.CreateDirectory(Path.Combine(resourcesDir, "gfx"));
        Directory.CreateDirectory(Path.Combine(resourcesDir, "scripts"));
        created.Add("resources/gfx/");
        created.Add("resources/scripts/");

        return $"Mod '{name}' scaffolded successfully!\nCreated files:\n" + string.Join('\n', created.Select(f => $"  - {f}"));
    }

    /// <summary>
    /// Escape a string for safe embedding in a Lua double-quoted string literal.
    /// Escapes backslash, double quote, newline, carriage return, and tab.
    /// </summary>
    private static string EscapeLuaString(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }
}
