using Microsoft.Extensions.AI;
using Moq;
using OpenStaff.Agent.Services;
using OpenStaff.Entities;
using OpenStaff.Mcp;
using OpenStaff.Mcp.BuiltinShell;
using OpenStaff.Mcp.Cli;
using System.Text.Json;

namespace OpenStaff.Tests.Unit;

public sealed class ShellBuiltinMcpToolProviderTests
{
    [Fact]
    public async Task GetPreloadSkipReasonAsync_ReturnsReason_WhenWorkspaceIsMissing()
    {
        var provider = CreateProvider();
        var connection = CreateConnection("""{"transportType":"builtin","builtinProvider":"shell","restrictToWorkspace":true}""");

        var reason = await provider.GetPreloadSkipReasonAsync(connection, CancellationToken.None);

        Assert.Equal("Builtin shell workspace is not configured.", reason);
    }

    [Fact]
    public async Task GetPreloadSkipReasonAsync_AllowsEmptyAllowlist()
    {
        var workspacePath = Directory.CreateTempSubdirectory("openstaff-shell-empty-allowlist").FullName;
        try
        {
            var provider = CreateProvider();
            var connection = CreateConnection(
                $$"""
                {"transportType":"builtin","builtinProvider":"shell","workspacePath":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","workingDirectory":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","restrictToWorkspace":true,"requiresApproval":true,"allowedExecutables":[]}
                """);

            var reason = await provider.GetPreloadSkipReasonAsync(connection, CancellationToken.None);

            Assert.Null(reason);
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task GetToolsAsync_ExposesSystemInfoTool()
    {
        var provider = CreateProvider();

        var tools = await provider.GetToolsAsync(CreateConnection("""{"transportType":"builtin","builtinProvider":"shell"}"""), CancellationToken.None);

        Assert.Contains(tools, tool => tool.Name == "shell.system_info");
    }

    [Fact]
    public async Task ShellExec_RunsWhitelistedProcessInsideWorkspace()
    {
        var workspacePath = Directory.CreateTempSubdirectory("openstaff-shell-workspace").FullName;
        try
        {
            var commandRunner = new Mock<ICommandRunner>(MockBehavior.Strict);
            commandRunner
                .Setup(runner => runner.RunAsync(
                    "dotnet",
                    It.Is<IReadOnlyList<string>>(args => args.Count == 1 && args[0] == "--version"),
                    workspacePath,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = "10.0.100",
                    StandardError = string.Empty
                });

            var provider = CreateProvider(commandRunner: commandRunner.Object);
            var function = await GetExecFunctionAsync(provider, CreateConnection(
                $$"""
                {"transportType":"builtin","builtinProvider":"shell","workspacePath":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","workingDirectory":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","restrictToWorkspace":true,"requiresApproval":false,"allowedExecutables":["dotnet"]}
                """));

            var result = await function.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new ShellBuiltinMcpToolProvider.ShellExecRequest(
                    Executable: "dotnet",
                    Args: ["--version"])
            }, CancellationToken.None);

            var payload = Assert.IsType<JsonElement>(result).Deserialize<ShellBuiltinMcpToolProvider.ShellExecResult>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            Assert.NotNull(payload);
            Assert.Equal(0, payload.ExitCode);
            Assert.Equal("10.0.100", payload.StandardOutput);
            Assert.Equal(workspacePath, payload.WorkingDirectory);
            commandRunner.VerifyAll();
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task ShellExec_RejectsWorkingDirectoryOutsideWorkspace()
    {
        var workspacePath = Directory.CreateTempSubdirectory("openstaff-shell-workspace").FullName;
        var outsidePath = Directory.CreateTempSubdirectory("openstaff-shell-outside").FullName;
        try
        {
            var provider = CreateProvider();
            var function = await GetExecFunctionAsync(provider, CreateConnection(
                $$"""
                {"transportType":"builtin","builtinProvider":"shell","workspacePath":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","workingDirectory":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","restrictToWorkspace":true,"requiresApproval":false,"allowedExecutables":["dotnet"]}
                """));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await function.InvokeAsync(new AIFunctionArguments
                {
                    ["request"] = new ShellBuiltinMcpToolProvider.ShellExecRequest(
                        Executable: "dotnet",
                        WorkingDirectory: outsidePath)
                }, CancellationToken.None);
            });

            Assert.Contains("escapes the configured workspace", error.Message);
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
            Directory.Delete(outsidePath, recursive: true);
        }
    }

    [Fact]
    public async Task ShellExec_RequestsApproval_WhenConfigured()
    {
        var workspacePath = Directory.CreateTempSubdirectory("openstaff-shell-approval").FullName;
        try
        {
            var permissionHandler = new Mock<IPermissionRequestHandler>(MockBehavior.Strict);
            permissionHandler
                .Setup(handler => handler.HandleAsync(
                    It.Is<PermissionAuthorizationRequest>(request =>
                        request.Kind == "shell"
                        && request.ToolName == "shell.exec"
                        && request.CommandText == "dotnet --version"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PermissionAuthorizationResult(PermissionAuthorizationKind.Reject));

            var provider = CreateProvider(permissionHandler: permissionHandler.Object);
            var function = await GetExecFunctionAsync(provider, CreateConnection(
                $$"""
                {"transportType":"builtin","builtinProvider":"shell","workspacePath":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","workingDirectory":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","restrictToWorkspace":true,"requiresApproval":true,"allowedExecutables":["dotnet"]}
                """));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await function.InvokeAsync(new AIFunctionArguments
                {
                    ["request"] = new ShellBuiltinMcpToolProvider.ShellExecRequest(
                        Executable: "dotnet",
                        Args: ["--version"])
                }, CancellationToken.None);
            });

            Assert.Equal("Shell command was rejected by permission policy.", error.Message);
            permissionHandler.VerifyAll();
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task ShellExec_RequestsApproval_WhenExecutableIsNotWhitelisted()
    {
        var workspacePath = Directory.CreateTempSubdirectory("openstaff-shell-nonallowlisted").FullName;
        try
        {
            var permissionHandler = new Mock<IPermissionRequestHandler>(MockBehavior.Strict);
            permissionHandler
                .Setup(handler => handler.HandleAsync(
                    It.Is<PermissionAuthorizationRequest>(request =>
                        request.Kind == "shell"
                        && request.ToolName == "shell.exec"
                        && request.CommandText == "powershell -NoProfile"
                        && request.Warning != null
                        && request.Warning.Contains("不在免审批白名单中", StringComparison.Ordinal)),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PermissionAuthorizationResult(PermissionAuthorizationKind.Accept));

            var commandRunner = new Mock<ICommandRunner>(MockBehavior.Strict);
            commandRunner
                .Setup(runner => runner.RunAsync(
                    "powershell",
                    It.Is<IReadOnlyList<string>>(args => args.Count == 1 && args[0] == "-NoProfile"),
                    workspacePath,
                    null,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CommandExecutionResult
                {
                    ExitCode = 0,
                    StandardOutput = "ok",
                    StandardError = string.Empty
                });

            var provider = CreateProvider(
                commandRunner: commandRunner.Object,
                permissionHandler: permissionHandler.Object);
            var function = await GetExecFunctionAsync(provider, CreateConnection(
                $$"""
                {"transportType":"builtin","builtinProvider":"shell","workspacePath":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","workingDirectory":"{{workspacePath.Replace("\\", "\\\\", StringComparison.Ordinal)}}","restrictToWorkspace":true,"requiresApproval":false,"allowedExecutables":["dotnet"]}
                """));

            var result = await function.InvokeAsync(new AIFunctionArguments
            {
                ["request"] = new ShellBuiltinMcpToolProvider.ShellExecRequest(
                    Executable: "powershell",
                    Args: ["-NoProfile"])
            }, CancellationToken.None);

            var payload = Assert.IsType<JsonElement>(result).Deserialize<ShellBuiltinMcpToolProvider.ShellExecResult>(
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            Assert.NotNull(payload);
            Assert.Equal(0, payload.ExitCode);
            Assert.Equal("ok", payload.StandardOutput);
            permissionHandler.VerifyAll();
            commandRunner.VerifyAll();
        }
        finally
        {
            Directory.Delete(workspacePath, recursive: true);
        }
    }

    [Fact]
    public async Task ShellSystemInfo_RequestsApproval_AndReturnsStructuredSnapshot()
    {
        var permissionHandler = new Mock<IPermissionRequestHandler>(MockBehavior.Strict);
        permissionHandler
            .Setup(handler => handler.HandleAsync(
                It.Is<PermissionAuthorizationRequest>(request =>
                    request.Kind == "shell"
                    && request.ToolName == "shell.system_info"
                    && request.CommandText == "system.info"
                    && request.Message.Contains("读取系统信息", StringComparison.Ordinal)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PermissionAuthorizationResult(PermissionAuthorizationKind.Accept));

        var provider = CreateProvider(permissionHandler: permissionHandler.Object);
        var function = await GetFunctionAsync(
            provider,
            CreateConnection("""{"transportType":"builtin","builtinProvider":"shell"}"""),
            "shell.system_info");

        var result = await function.InvokeAsync(new AIFunctionArguments
        {
            ["request"] = new ShellBuiltinMcpToolProvider.ShellSystemInfoRequest(
                IncludeInstalledSoftware: false,
                MaxInstalledSoftware: 0)
        }, CancellationToken.None);

        var payload = Assert.IsType<JsonElement>(result).Deserialize<ShellBuiltinMcpToolProvider.ShellSystemInfoResult>(
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.System.HostName));
        Assert.True(payload.Processor.LogicalProcessorCount > 0);
        Assert.NotNull(payload.NetworkAdapters);
        Assert.Equal("skipped", payload.Software.Source);
        permissionHandler.VerifyAll();
    }

    private static ShellBuiltinMcpToolProvider CreateProvider(
        ICommandRunner? commandRunner = null,
        IPermissionRequestHandler? permissionHandler = null)
    {
        commandRunner ??= Mock.Of<ICommandRunner>();
        permissionHandler ??= Mock.Of<IPermissionRequestHandler>();
        return new ShellBuiltinMcpToolProvider(commandRunner, permissionHandler);
    }

    private static ResolvedMcpClientConnection CreateConnection(string configurationJson)
        => new(
            CacheKey: "builtin:shell:test",
            ServerId: Guid.NewGuid(),
            Name: "OpenStaff Shell",
            TransportType: McpTransportTypes.Builtin,
            ConnectionConfigJson: configurationJson,
            EnvironmentVariables: null);

    private static async Task<AIFunction> GetExecFunctionAsync(
        ShellBuiltinMcpToolProvider provider,
        ResolvedMcpClientConnection connection)
        => await GetFunctionAsync(provider, connection, "shell.exec");

    private static async Task<AIFunction> GetFunctionAsync(
        ShellBuiltinMcpToolProvider provider,
        ResolvedMcpClientConnection connection,
        string toolName)
    {
        var tool = Assert.Single((await provider.GetToolsAsync(connection, CancellationToken.None)).Where(item => item.Name == toolName));
        return Assert.IsAssignableFrom<AIFunction>(tool.Tool);
    }
}
