using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using OpenStaff.Application.Skills.Services;

namespace OpenStaff.Tests.Unit;

#pragma warning disable MAAI001
public sealed class PowerShellAgentSkillScriptRunnerTests
{
    [Fact]
    public async Task RunAsync_ShouldExecuteRepositorySkillAndReturnStdout()
    {
        var runner = new PowerShellAgentSkillScriptRunner(NullLogger<PowerShellAgentSkillScriptRunner>.Instance);
        var skill = CreateSkill(out var script);
        var arguments = new AIFunctionArguments
        {
            ["command"] = "Write-Output 'hello from skill'"
        };

        var result = await runner.RunAsync(skill, script, arguments, CancellationToken.None);

        var payload = Assert.IsType<JsonElement>(result);
        Assert.Equal(0, payload.GetProperty("exitCode").GetInt32());
        Assert.False(payload.GetProperty("timedOut").GetBoolean());
        Assert.Contains("hello from skill", payload.GetProperty("stdout").GetString(), StringComparison.Ordinal);
        Assert.True(string.IsNullOrWhiteSpace(payload.GetProperty("hostError").GetString()));
    }

    [Fact]
    public async Task RunAsync_ShouldSurfaceFailedCommandExitCode()
    {
        var runner = new PowerShellAgentSkillScriptRunner(NullLogger<PowerShellAgentSkillScriptRunner>.Instance);
        var skill = CreateSkill(out var script);
        var arguments = new AIFunctionArguments
        {
            ["command"] = "throw 'boom'"
        };

        var result = await runner.RunAsync(skill, script, arguments, CancellationToken.None);

        var payload = Assert.IsType<JsonElement>(result);
        Assert.Equal(1, payload.GetProperty("exitCode").GetInt32());
        Assert.Contains("boom", payload.GetProperty("stderr").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.True(string.IsNullOrWhiteSpace(payload.GetProperty("hostError").GetString()));
    }

    [Fact]
    public async Task RunAsync_ShouldReturnTimeoutResultWhenCommandTakesTooLong()
    {
        var runner = new PowerShellAgentSkillScriptRunner(NullLogger<PowerShellAgentSkillScriptRunner>.Instance);
        var skill = CreateSkill(out var script);
        var arguments = new AIFunctionArguments
        {
            ["command"] = "Start-Sleep -Seconds 3",
            ["timeoutSeconds"] = 1
        };

        var result = await runner.RunAsync(skill, script, arguments, CancellationToken.None);

        var payload = Assert.IsType<JsonElement>(result);
        Assert.Equal(124, payload.GetProperty("exitCode").GetInt32());
        Assert.True(payload.GetProperty("timedOut").GetBoolean());
    }

    private static AgentFileSkill CreateSkill(out AgentFileSkillScript script)
    {
        var repositoryRoot = FindRepositoryRoot();
        var skillRoot = Path.Combine(repositoryRoot, "skills", "run-powershell");
        var scriptPath = Path.Combine(skillRoot, "scripts", "run-command.ps1");
        script = (AgentFileSkillScript)Activator.CreateInstance(
            typeof(AgentFileSkillScript),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: ["run-command", scriptPath, null],
            culture: null)!;

        IReadOnlyList<AgentSkillResource> resources = Array.Empty<AgentSkillResource>();
        IReadOnlyList<AgentSkillScript> scripts = [script];

        return (AgentFileSkill)Activator.CreateInstance(
            typeof(AgentFileSkill),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args:
            [
            new AgentSkillFrontmatter("run-powershell", "Execute arbitrary PowerShell commands.", "Requires pwsh on PATH."),
            skillRoot,
            File.ReadAllText(Path.Combine(skillRoot, "SKILL.md")),
            resources,
            scripts
            ],
            culture: null)!;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CLAUDE.md")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be located.");
    }
}
#pragma warning restore MAAI001
