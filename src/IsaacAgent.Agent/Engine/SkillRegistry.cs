using IsaacAgent.Core.Services;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent.Engine;

/// <summary>
/// Manages skill registration and activation. Skills are evaluated
/// per-request to determine which ones should augment the system prompt
/// and pre-fetch RAG context.
/// </summary>
public sealed class SkillRegistry
{
    private readonly List<ISkill> _skills = [];
    private readonly ILogger<SkillRegistry>? _logger;

    public IReadOnlyList<ISkill> All => _skills.AsReadOnly();

    public SkillRegistry(ILogger<SkillRegistry>? logger = null)
    {
        _logger = logger;
    }

    public void Register(ISkill skill)
    {
        _skills.Add(skill);
        _logger?.LogInformation("Registered skill: {SkillName}", skill.Name);
    }

    public void RegisterAll(IEnumerable<ISkill> skills)
    {
        foreach (var skill in skills)
            Register(skill);
    }

    /// <summary>
    /// Finds a skill by its slash command (e.g. "/create-item" → skill).
    /// Returns null if no skill matches.
    /// </summary>
    public ISkill? FindBySlashCommand(string command)
    {
        var normalized = command.Trim().ToLowerInvariant();
        return _skills.FirstOrDefault(s =>
            s.SlashCommand is { } cmd && cmd.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns skills that should auto-activate for the given user message
    /// and project context. Excludes skills already triggered by slash command.
    /// </summary>
    public List<ISkill> GetAutoActivatedSkills(string userMessage, string? projectDir)
    {
        return _skills
            .Where(s => s.ShouldActivate(userMessage, projectDir))
            .ToList();
    }

    /// <summary>
    /// Returns all skill descriptions for the command palette.
    /// </summary>
    public List<SkillDescriptor> GetDescriptors() =>
        _skills.Select(s => new SkillDescriptor(s.Name, s.DisplayName, s.Description, s.SlashCommand)).ToList();
}

public sealed record SkillDescriptor(string Name, string DisplayName, string Description, string? SlashCommand);
