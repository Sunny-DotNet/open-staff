using Moq;
using OpenStaff.Agent.Tools;
using OpenStaff.Core.Agents;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class AgentToolRegistryTests
{
    private static IAgentTool CreateMockTool(string name, string description = "Test tool")
    {
        var mock = new Mock<IAgentTool>();
        mock.Setup(t => t.Name).Returns(name);
        mock.Setup(t => t.Description).Returns(description);
        mock.Setup(t => t.ParametersSchema).Returns("{}");
        return mock.Object;
    }

    [Fact]
    public void Register_AndGetTool_ReturnsSameTool()
    {
        var registry = new AgentToolRegistry();
        var tool = CreateMockTool("create_file");

        registry.Register(tool);
        var retrieved = registry.GetTool("create_file");

        Assert.NotNull(retrieved);
        Assert.Equal("create_file", retrieved!.Name);
    }

    [Fact]
    public void GetTool_UnregisteredName_ReturnsNull()
    {
        var registry = new AgentToolRegistry();

        var result = registry.GetTool("nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public void GetTool_CaseInsensitiveLookup()
    {
        var registry = new AgentToolRegistry();
        var tool = CreateMockTool("Create_File");

        registry.Register(tool);

        Assert.NotNull(registry.GetTool("create_file"));
        Assert.NotNull(registry.GetTool("CREATE_FILE"));
        Assert.NotNull(registry.GetTool("Create_File"));
    }

    [Fact]
    public void GetTools_ReturnsCorrectSubset()
    {
        var registry = new AgentToolRegistry();
        registry.Register(CreateMockTool("tool_a"));
        registry.Register(CreateMockTool("tool_b"));
        registry.Register(CreateMockTool("tool_c"));

        var result = registry.GetTools(new[] { "tool_a", "tool_c" });

        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.Name == "tool_a");
        Assert.Contains(result, t => t.Name == "tool_c");
    }

    [Fact]
    public void GetTools_SkipsUnknownNames()
    {
        var registry = new AgentToolRegistry();
        registry.Register(CreateMockTool("tool_a"));

        var result = registry.GetTools(new[] { "tool_a", "unknown_tool" });

        Assert.Single(result);
        Assert.Equal("tool_a", result[0].Name);
    }

    [Fact]
    public void GetTools_EmptyNames_ReturnsEmptyList()
    {
        var registry = new AgentToolRegistry();
        registry.Register(CreateMockTool("tool_a"));

        var result = registry.GetTools(Array.Empty<string>());

        Assert.Empty(result);
    }

    [Fact]
    public void GetAllTools_ReturnsAllRegistered()
    {
        var registry = new AgentToolRegistry();
        registry.Register(CreateMockTool("tool_a"));
        registry.Register(CreateMockTool("tool_b"));
        registry.Register(CreateMockTool("tool_c"));

        var all = registry.GetAllTools();

        Assert.Equal(3, all.Count);
    }

    [Fact]
    public void GetAllTools_EmptyRegistry_ReturnsEmpty()
    {
        var registry = new AgentToolRegistry();

        var all = registry.GetAllTools();

        Assert.Empty(all);
    }

    [Fact]
    public void Register_OverwritesExistingToolWithSameName()
    {
        var registry = new AgentToolRegistry();
        var original = CreateMockTool("tool_a", "original");
        var updated = CreateMockTool("tool_a", "updated");

        registry.Register(original);
        registry.Register(updated);

        var retrieved = registry.GetTool("tool_a");
        Assert.Equal("updated", retrieved!.Description);
    }

    [Fact]
    public void GetTools_CaseInsensitive_MatchesMixedCase()
    {
        var registry = new AgentToolRegistry();
        registry.Register(CreateMockTool("MyTool"));

        var result = registry.GetTools(new[] { "mytool" });

        Assert.Single(result);
    }

    [Fact]
    public void ImplementsIAgentToolRegistry()
    {
        var registry = new AgentToolRegistry();
        Assert.IsAssignableFrom<IAgentToolRegistry>(registry);
    }
}
