using OpenStaff.Agents.Roles;
using OpenStaff.Core.Models;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class RoleConfigLoaderTests
{
    [Fact]
    public void LoadBuiltin_ReturnsOnlySecretary()
    {
        var configs = RoleConfigLoader.LoadBuiltin();

        Assert.Single(configs);
        Assert.Equal(BuiltinRoleTypes.Secretary, configs[0].RoleType);
    }

    [Fact]
    public void LoadAll_ReturnsAllEmbeddedConfigs()
    {
        var configs = RoleConfigLoader.LoadAll();

        // 9 total: secretary + 8 legacy templates
        Assert.Equal(9, configs.Count);
    }

    [Fact]
    public void LoadAll_ContainsSecretary()
    {
        var configs = RoleConfigLoader.LoadAll();
        var roleTypes = configs.Select(c => c.RoleType).ToHashSet();

        Assert.Contains(BuiltinRoleTypes.Secretary, roleTypes);
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
    public void LoadBuiltin_SecretaryHasExpectedConfig()
    {
        var configs = RoleConfigLoader.LoadBuiltin();
        var secretary = configs.First(c => c.RoleType == BuiltinRoleTypes.Secretary);

        Assert.Equal("秘书", secretary.Name);
        Assert.NotNull(secretary.Routing);
    }

    [Fact]
    public void LoadAll_EachConfigHasModelParameters()
    {
        var configs = RoleConfigLoader.LoadAll();

        Assert.All(configs, config =>
        {
            if (config.ModelParameters != null)
            {
                Assert.True(config.ModelParameters.Temperature >= 0);
                Assert.True(config.ModelParameters.MaxTokens > 0);
            }
        });
    }
}
