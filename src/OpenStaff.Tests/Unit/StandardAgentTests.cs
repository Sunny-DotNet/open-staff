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
        ProviderAccount = new ProviderAccount { ProtocolType = "openai", Name = "Test" },
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
    public void CreateAgent_ReturnsAIAgent()
    {
        var provider = CreateProvider();
        var role = CreateRole();

        var agent = provider.CreateAgent(role);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }

    [Fact]
    public void CreateAgent_ThrowsWithoutProviderAccount()
    {
        var provider = CreateProvider();
        var role = new AgentRole { RoleType = "secretary", Name = "Secretary" };

        Assert.Throws<InvalidOperationException>(() => provider.CreateAgent(role));
    }

    [Fact]
    public void CreateAgent_ThrowsWithoutApiKey()
    {
        var provider = CreateProvider();
        var role = new AgentRole
        {
            RoleType = "secretary",
            Name = "Secretary",
            ProviderAccount = new ProviderAccount { ProtocolType = "openai", Name = "Test" }
        };

        Assert.Throws<InvalidOperationException>(() => provider.CreateAgent(role));
    }

    [Fact]
    public void CreateAgent_UsesDbRoleConfigForCustomRole()
    {
        var provider = CreateProvider();
        var role = new AgentRole
        {
            RoleType = "my_custom_role",
            Name = "Custom Role",
            SystemPrompt = "You are a custom agent.",
            ModelName = "gpt-4o",
            ProviderAccount = new ProviderAccount { ProtocolType = "openai", Name = "Test" },
            ApiKey = "test-api-key"
        };

        var agent = provider.CreateAgent(role);
        Assert.NotNull(agent);
        Assert.IsAssignableFrom<AIAgent>(agent);
    }
}
