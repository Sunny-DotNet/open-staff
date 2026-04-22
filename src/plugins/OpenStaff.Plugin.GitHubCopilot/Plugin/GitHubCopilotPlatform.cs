using Microsoft.Extensions.DependencyInjection;
using OpenHub.Agents;
using OpenStaff.Agent;
using OpenStaff.Agents;
using OpenStaff.Agent.Vendor.GitHubCopilot;
using OpenStaff.Entities;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Plugin;

public sealed class GitHubCopilotPlatform(
    IServiceProvider serviceProvider)
    : ServiceBase(serviceProvider),
      IPlatform,
      IAgentProvider,
      IHasProtocol,
      IHasTaskAgentFactory,
    IHasChatClientFactory
    
{
    private static readonly IPlatformTaskAgentFactory TaskAgentFactoryCapability =
        new PlatformTaskAgentFactoryCapability(typeof(GitHubCopilotTaskAgentFactory));

    public string PlatformKey => "github-copilot";
    public string ProviderType => PlatformKey;
    public string DisplayName => "GitHub Copilot";

    public IProtocol GetProtocol() => CreateInstance<GitHubCopilotProtocol>();


    public IPlatformTaskAgentFactory GetTaskAgentFactory() => TaskAgentFactoryCapability;

    public async Task<IStaffAgent> CreateAgentAsync(AgentRole role, OpenStaff.Core.Agents.AgentContext context)
    {
        var taskAgentFactory = TaskAgentFactoryCapability.ResolveFactory(serviceProvider);
        var taskAgent = await taskAgentFactory.CreateAsync(
            new TaskAgentCreateRequest(
                new TaskAgentRole(
                    role.Id,
                    role.Name,
                    role.Description,
                    role.JobTitle,
                    role.ProviderType,
                    role.ModelProviderId,
                    role.ModelName,
                    null,
                    role.Config),
                new TaskAgentContext(
                    context.ProjectId,
                    context.SessionId,
                    context.AgentInstanceId,
                    context.Project?.Name,
                    context.Project?.WorkspacePath,
                    context.Scene?.ToString(),
                    context.GetResolvedSkillDirectories()),
                role,
                context));

        return taskAgent is IStaffAgent staffAgent
            ? staffAgent
            : taskAgent.AsStaffAgent(serviceProvider, this);
    }

    public IChatClientFactory GetChatClientFactory() => CreateInstance<GitHubCopilotChatClientFactory>();
}
