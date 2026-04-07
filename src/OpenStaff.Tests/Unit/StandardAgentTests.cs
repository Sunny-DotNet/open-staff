using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent.Builtin;
using OpenStaff.Agent.Tools;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class BuiltinAgentProviderTests
{
    private static BuiltinAgentProvider CreateProvider()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var toolRegistry = new AgentToolRegistry();
        var chatClientFactory = new ChatClientFactory(services.GetRequiredService<ILoggerFactory>());
        var promptLoader = new OpenStaff.Agent.Builtin.Prompts.EmbeddedPromptLoader();
        return new BuiltinAgentProvider(services, toolRegistry, chatClientFactory, promptLoader, services.GetRequiredService<ILoggerFactory>());
    }

    private static AgentRole CreateRole(string roleType = "secretary") => new()
    {
        RoleType = roleType,
        Name = roleType,
    };

    private static ResolvedProvider CreateResolvedProvider() => new()
    {
        Account = new ProviderAccount { ProtocolType = "openai", Name = "Test" },
        ApiKey = "test-api-key"
    };

    [Fact]
    public void ProviderType_IsBuiltin()
    {
        var provider = CreateProvider();
        Assert.Equal("builtin", provider.ProviderType);
    }

    [Fact]
    public void GetRoleConfig_ReturnsConfigForBuiltinRole()
    {
        var provider = CreateProvider();
        var config = provider.GetRoleConfig("secretary");
        Assert.NotNull(config);
        Assert.Equal("secretary", config!.RoleType);
    }

    [Fact]
    public void GetRoleConfig_ReturnsNullForUnknownRole()
    {
        var provider = CreateProvider();
        var config = provider.GetRoleConfig("nonexistent");
        Assert.Null(config);
    }

    [Fact]
    public void RoleConfigs_ContainsBuiltinRoles()
    {
        var provider = CreateProvider();
        Assert.True(provider.RoleConfigs.Count > 0);
    }

    [Fact]
    public async Task CreateAgent_ReturnsAIAgent()
    {
        var provider = CreateProvider();
        var role = CreateRole();
        var resolved = CreateResolvedProvider();
        var context = new AgentContext { Role = role };

        var agent = await provider.CreateAgentAsync(role, context, resolved);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public async Task CreateAgent_ThrowsWithoutProviderAccount()
    {
        var provider = CreateProvider();
        var role = new AgentRole { RoleType = "secretary", Name = "Secretary" };
        var resolved = new ResolvedProvider { ApiKey = "k" };
        var context = new AgentContext { Role = role };

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateAgentAsync(role, context, resolved));
    }

    [Fact]
    public async Task CreateAgent_ThrowsWithoutApiKey()
    {
        var provider = CreateProvider();
        var role = new AgentRole
        {
            RoleType = "secretary",
            Name = "Secretary",
        };
        var resolved = new ResolvedProvider
        {
            Account = new ProviderAccount { ProtocolType = "openai", Name = "Test" }
        };
        var context = new AgentContext { Role = role };

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CreateAgentAsync(role, context, resolved));
    }

    [Fact]
    public async Task CreateAgent_UsesDbRoleConfigForCustomRole()
    {
        var provider = CreateProvider();
        var role = new AgentRole
        {
            RoleType = "my_custom_role",
            Name = "Custom Role",
            SystemPrompt = "You are a custom agent.",
            ModelName = "gpt-4o",
        };
        var resolved = CreateResolvedProvider();
        var context = new AgentContext { Role = role };

        var agent = await provider.CreateAgentAsync(role, context, resolved);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }
}
