using Microsoft.Extensions.DependencyInjection;
using OpenHub.Agents;
using OpenStaff.Agent;
using OpenStaff.Agents;
using OpenStaff.Plugin.Anthropic;
using OpenStaff.Entities;
using OpenStaff.Provider.Platforms;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Platform;

public sealed class AnthropicPlatform(    IServiceProvider serviceProvider)
    : IPlatform, IAgentProvider, IHasProtocol, IHasChatClientFactory, IHasTaskAgentFactory
{

    private static readonly IPlatformTaskAgentFactory TaskAgentFactoryCapability =
        new PlatformTaskAgentFactoryCapability(typeof(AnthropicTaskAgentFactory));

    public string PlatformKey => "anthropic";
    public string ProviderType => PlatformKey;
    public string DisplayName => "Anthropic";

    public IProtocol GetProtocol() => ActivatorUtilities.CreateInstance<AnthropicProtocol>(serviceProvider);

    public IChatClientFactory GetChatClientFactory() => ActivatorUtilities.CreateInstance<AnthropicChatClientFactory>(serviceProvider);

    public IPlatformTaskAgentFactory GetTaskAgentFactory()=> TaskAgentFactoryCapability;

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
}
