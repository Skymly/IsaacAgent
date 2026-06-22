using IsaacAgent.Core.Services;
using IsaacAgent.Agent.Engine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent;

public static class AgentServiceRegistration
{
    public static IServiceCollection AddIsaacAgent(this IServiceCollection services)
    {
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ToolRegistry>>();
            var retriever = sp.GetService<IRetriever>();
            var registry = new ToolRegistry(logger, retriever);
            registry.ReconfigureForProject(null);
            return registry;
        });

        // AgentSession is created per-session via the factory, not as a singleton,
        // so multiple windows/projects can have independent sessions.
        services.AddTransient<AgentSession>(sp =>
        {
            var chat = sp.GetRequiredService<IChatService>();
            var tools = sp.GetRequiredService<ToolRegistry>();
            var logger = sp.GetRequiredService<ILogger<AgentSession>>();
            return new AgentSession(chat, tools, null, logger);
        });

        services.AddSingleton<IAgentSessionFactory, AgentSessionFactory>();

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
        var tools = _services.GetRequiredService<ToolRegistry>();
        var logger = _services.GetRequiredService<ILogger<AgentSession>>();
        return new AgentSession(chat, tools, projectDir, logger);
    }
}
