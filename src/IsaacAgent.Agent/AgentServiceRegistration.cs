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
            var registry = new ToolRegistry(logger);
            registry.ReconfigureForProject(null);
            return registry;
        });

        services.AddSingleton<AgentSession>(sp =>
        {
            var chat = sp.GetRequiredService<IChatService>();
            var tools = sp.GetRequiredService<ToolRegistry>();
            var logger = sp.GetRequiredService<ILogger<AgentSession>>();
            return new AgentSession(chat, tools, null, logger);
        });

        return services;
    }
}
