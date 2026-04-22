using System.Text.Json.Nodes;
using OpenStaff.Application.McpServers.Services;
using OpenStaff.Entities;

namespace OpenStaff.Tests.Unit;

public class McpRuntimeParameterDefaultsServiceTests
{
    [Fact]
    public void CreateHostDefaults_ForFilesystem_UsesManagedTemporaryDirectory()
    {
        var service = new McpRuntimeParameterDefaultsService();

        var runtimeParameters = service.CreateHostDefaults(CreateFilesystemServer());

        var workspacePath = runtimeParameters["workspacePath"]?.GetValue<string>();
        Assert.Equal(Path.Combine(Path.GetTempPath(), "openstaff-mcp", "filesystem"), workspacePath);
        Assert.Equal(workspacePath, runtimeParameters["workingDirectory"]?.GetValue<string>());
        Assert.True(Directory.Exists(workspacePath));
        Assert.Null(runtimeParameters["workspace"]);
        Assert.Null(runtimeParameters["workspaces"]);
    }

    [Fact]
    public void CreateProjectDefaults_ForFilesystem_UsesProjectWorkspace()
    {
        var service = new McpRuntimeParameterDefaultsService();

        var runtimeParameters = service.CreateProjectDefaults(CreateFilesystemServer(), @"A:\projects\demo");

        Assert.Equal(@"A:\projects\demo", runtimeParameters["workspacePath"]?.GetValue<string>());
        Assert.Equal(@"A:\projects\demo", runtimeParameters["workingDirectory"]?.GetValue<string>());
        Assert.Null(runtimeParameters["workspace"]);
        Assert.Null(runtimeParameters["workspaces"]);
    }

    [Fact]
    public void RewriteForProjectContext_RewritesFilesystemWorkspaceKeys()
    {
        var service = new McpRuntimeParameterDefaultsService();
        var source = new JsonObject
        {
            ["workspace"] = @"A:\seed-role",
            ["workspaces"] = new JsonArray(@"A:\seed-role", @"A:\seed-role-2")
        };

        var runtimeParameters = service.RewriteForProjectContext(CreateFilesystemServer(), source, @"A:\projects\demo");

        Assert.Equal(@"A:\projects\demo", runtimeParameters["workspacePath"]?.GetValue<string>());
        Assert.Equal(@"A:\projects\demo", runtimeParameters["workingDirectory"]?.GetValue<string>());
        Assert.Null(runtimeParameters["workspace"]);
        Assert.Null(runtimeParameters["workspaces"]);
    }

    private static McpServer CreateFilesystemServer()
        => new()
        {
            Name = "Filesystem",
            NpmPackage = "@modelcontextprotocol/server-filesystem",
            IsEnabled = true
        };
}
