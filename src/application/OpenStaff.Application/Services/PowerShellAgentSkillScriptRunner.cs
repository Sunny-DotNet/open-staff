using System.Diagnostics;
using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Agent;

namespace OpenStaff.Application.Skills.Services;

#pragma warning disable MAAI001
/// <summary>
/// 负责在宿主进程中执行基于 PowerShell 的文件型 skill 脚本。
/// </summary>
public sealed class PowerShellAgentSkillScriptRunner : IAgentSkillScriptRunner
{
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ILogger<PowerShellAgentSkillScriptRunner> _logger;

    public PowerShellAgentSkillScriptRunner(ILogger<PowerShellAgentSkillScriptRunner> logger)
    {
        _logger = logger;
    }

    public IReadOnlyList<string> AllowedScriptExtensions { get; } = [".ps1"];

    public async Task<object?> RunAsync(
        AgentFileSkill skill,
        AgentFileSkillScript script,
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(script);
        ArgumentNullException.ThrowIfNull(arguments);

        var extension = Path.GetExtension(script.FullPath);
        if (!string.Equals(extension, ".ps1", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Skill script '{script.FullPath}' is not supported by the PowerShell runner.");

        var serializedArguments = JsonSerializer.Serialize(
            arguments.ToDictionary(
                pair => pair.Key,
                pair => NormalizeArgumentValue(pair.Value),
                StringComparer.OrdinalIgnoreCase));

        var startInfo = new ProcessStartInfo
        {
            FileName = "pwsh",
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script.FullPath);
        startInfo.ArgumentList.Add("-ArgumentsJson");
        startInfo.ArgumentList.Add(serializedArguments);

        using var process = new Process
        {
            StartInfo = startInfo
        };

        _logger.LogDebug("Executing PowerShell skill script {ScriptPath}", script.FullPath);

        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PowerShell skill script {ScriptPath}", script.FullPath);
            throw;
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            _logger.LogWarning(
                "PowerShell skill script {ScriptPath} failed with exit code {ExitCode}. stderr: {Stderr}",
                script.FullPath,
                process.ExitCode,
                stderr);
            throw new InvalidOperationException(
                $"Skill script '{script.FullPath}' failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"stderr: {stderr}{Environment.NewLine}stdout: {stdout}");
        }

        if (string.IsNullOrWhiteSpace(stdout))
            return new { stdout, stderr };

        try
        {
            return JsonSerializer.Deserialize<object>(stdout, ResultJsonOptions);
        }
        catch (JsonException)
        {
            return stdout;
        }
    }

    private static object? NormalizeArgumentValue(object? value)
        => value switch
        {
            null => null,
            JsonElement jsonElement => NormalizeJsonElement(jsonElement),
            _ => value
        };

    private static object? NormalizeJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => NormalizeJsonElement(property.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(NormalizeJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
    {
    }
}
#pragma warning restore MAAI001
}
