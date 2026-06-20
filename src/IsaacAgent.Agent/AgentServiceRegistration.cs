using IsaacAgent.Core.Services;
using IsaacAgent.Agent.Engine;
using IsaacAgent.Tools.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IsaacAgent.Agent;

public static class AgentServiceRegistration
{
    public static IServiceCollection AddIsaacAgent(this IServiceCollection services, string? projectDir)
    {
        services.AddSingleton<ToolRegistry>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ToolRegistry>>();
            var registry = new ToolRegistry(logger);

            if (projectDir is not null)
            {
                Directory.CreateDirectory(projectDir);
                registry.RegisterAll([
                    new ReadFileTool(projectDir),
                    new WriteFileTool(projectDir),
                    new ListFilesTool(projectDir),
                    new SearchApiTool(),
                    new GetCallbackInfoTool(),
                    new GetClassInfoTool(),
                    new DiagnoseLuaTool(projectDir),
                    new ScaffoldModTool(projectDir)
                ]);
            }
            else
            {
                registry.RegisterAll([
                    new SearchApiTool(),
                    new GetCallbackInfoTool(),
                    new GetClassInfoTool()
                ]);
            }

            return registry;
        });

        services.AddSingleton<AgentSession>(sp =>
        {
            var chat = sp.GetRequiredService<IChatService>();
            var tools = sp.GetRequiredService<ToolRegistry>();
            var logger = sp.GetRequiredService<ILogger<AgentSession>>();
            return new AgentSession(chat, tools, projectDir, logger);
        });

        return services;
    }
}
