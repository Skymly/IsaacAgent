using IsaacAgent.Core.Services;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Agent.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent;

public static class AgentServiceRegistration
{
    public static IServiceCollection AddIsaacAgent(this IServiceCollection services)
    {
        // ToolRegistry is NOT registered as a singleton — each AgentSession
        // gets its own instance via the factory so multiple tabs don't share
        // (and clobber) the same tool set when switching projects.
        services.AddSingleton<IAgentSessionFactory, AgentSessionFactory>();

        // SkillRegistry is a singleton — skills are stateless and shared
        // across all sessions/tabs.
        services.AddSingleton<SkillRegistry>(sp =>
        {
            var logger = sp.GetService<ILogger<SkillRegistry>>();
            var registry = new SkillRegistry(logger);
            registry.RegisterAll([
                new CreateCollectibleSkill(),
                new CreateFamiliarSkill(),
                new DebugFromLogSkill(),
                new ValidateProjectSkill(),
                new AddCallbackSkill(),
                new AddSaveDataSkill(),
                new AddTrinketSkill(),
                new AddCardSkill(),
                new AddPillSkill(),
                new AddBossSkill()
            ]);
            return registry;
        });

        return services;
    }
}

/// <summary>
/// Factory for creating independent <see cref="AgentSession"/> instances.
/// Each call returns a new session with its own history and project context.
/// </summary>
public interface IAgentSessionFactory
{
    AgentSession Create(string? projectDir = null);
}

internal sealed class AgentSessionFactory : IAgentSessionFactory
{
    private readonly IServiceProvider _services;

    public AgentSessionFactory(IServiceProvider services)
    {
        _services = services;
    }

    public AgentSession Create(string? projectDir = null)
    {
        var chat = _services.GetRequiredService<IChatService>();
        var toolLogger = _services.GetRequiredService<ILogger<ToolRegistry>>();
        var retriever = _services.GetService<IRetriever>();
        var tools = new ToolRegistry(toolLogger, retriever);
        var skills = _services.GetRequiredService<SkillRegistry>();
        var logger = _services.GetRequiredService<ILogger<AgentSession>>();
        return new AgentSession(chat, tools, projectDir, logger, skills, retriever);
    }
}
