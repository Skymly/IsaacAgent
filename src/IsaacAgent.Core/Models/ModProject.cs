namespace IsaacAgent.Core.Models;

public sealed class ModProject
{
    public required string Name { get; set; }
    public required string DirectoryPath { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public int ApiVersion { get; set; } = 1;
    public string? Version { get; set; }
    public List<string> Tags { get; set; } = [];
    public ModProjectStatus Status { get; set; } = ModProjectStatus.Draft;
}

public enum ModProjectStatus
{
    Draft,
    Active,
    Built,
    Published
}

public sealed class ModFile
{
    public required string RelativePath { get; init; }
    public required string Content { get; set; }
    public ModFileType Type { get; init; }

    public static ModFileType InferType(string path) => path switch
    {
        _ when path.EndsWith(".lua") => ModFileType.Lua,
        _ when path.EndsWith(".xml") => ModFileType.Xml,
        _ when path.EndsWith("metadata.xml") => ModFileType.Metadata,
        _ when path.EndsWith(".png") => ModFileType.Sprite,
        _ => ModFileType.Other
    };
}

public enum ModFileType
{
    Lua,
    Xml,
    Metadata,
    Sprite,
    Other
}
