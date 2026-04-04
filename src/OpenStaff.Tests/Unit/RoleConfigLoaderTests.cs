using OpenStaff.Agents.Roles;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class RoleConfigLoaderTests
{
    [Fact]
    public void LoadAll_Returns8RoleConfigs()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.Equal(8, configs.Count);
    }

    [Fact]
    public void LoadAll_ContainsAllExpectedRoleTypes()
    {
        var configs = RoleConfigLoader.LoadAll();
        var roleTypes = configs.Select(c => c.RoleType).ToHashSet();

        Assert.Contains(BuiltinRoleTypes.Orchestrator, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Communicator, roleTypes);
        Assert.Contains(BuiltinRoleTypes.DecisionMaker, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Architect, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Producer, roleTypes);
        Assert.Contains(BuiltinRoleTypes.Debugger, roleTypes);
        Assert.Contains(BuiltinRoleTypes.ImageCreator, roleTypes);
        Assert.Contains(BuiltinRoleTypes.VideoCreator, roleTypes);
    }

    [Fact]
    public void LoadAll_EachConfigHasRoleType()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            Assert.False(string.IsNullOrEmpty(config.RoleType));
        });
    }

    [Fact]
    public void LoadAll_EachConfigHasName()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            Assert.False(string.IsNullOrEmpty(config.Name));
        });
    }

    [Fact]
    public void LoadAll_EachConfigHasSystemPrompt()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            Assert.False(string.IsNullOrEmpty(config.SystemPrompt));
        });
    }

    [Fact]
    public void LoadAll_EachConfigHasToolsList()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            Assert.NotNull(config.Tools);
        });
    }

    [Fact]
    public void LoadAll_SystemPromptFollowsNamingConvention()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            // System prompt should be "{roleType}.system"
            Assert.Equal($"{config.RoleType}.system", config.SystemPrompt);
        });
    }

    [Fact]
    public void LoadAll_AllBuiltinConfigsAreMarkedBuiltin()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            Assert.True(config.IsBuiltin);
        });
    }

    [Fact]
    public void LoadAll_RoleTypesAreUnique()
    {
        var configs = RoleConfigLoader.LoadAll();
        var roleTypes = configs.Select(c => c.RoleType).ToList();

        Assert.Equal(roleTypes.Count, roleTypes.Distinct().Count());
    }

    [Fact]
    public void LoadAll_CommunicatorConfigHasExpectedRouting()
    {
        var configs = RoleConfigLoader.LoadAll();
        var communicator = configs.First(c => c.RoleType == BuiltinRoleTypes.Communicator);

        Assert.NotNull(communicator.Routing);
        Assert.NotNull(communicator.Routing!.Markers);
        Assert.Contains("REQUIREMENTS_COMPLETE", communicator.Routing.Markers.Keys);
    }

    [Fact]
    public void LoadAll_EachConfigHasModelParameters()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            // ModelParameters may be null for some configs, but if set should be valid
            if (config.ModelParameters != null)
            {
                Assert.True(config.ModelParameters.Temperature >= 0);
                Assert.True(config.ModelParameters.MaxTokens > 0);
            }
        });
    }
}
