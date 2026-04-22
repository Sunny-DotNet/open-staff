using System.Diagnostics;
using System.Text;

namespace OpenStaff.Mcp.Cli;

/// <summary>
/// zh-CN: 通过 <see cref="Process"/> 执行命令，并完整捕获标准输出/错误输出。
/// en: Executes commands via <see cref="Process"/> and captures both stdout and stderr.
/// </summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    public async Task<CommandExecutionResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedFileName = ExecutablePathResolver.ResolveExecutablePath(fileName, workingDirectory, environmentVariables);
        var startInfo = new ProcessStartInfo
        {
            FileName = resolvedFileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (!string.IsNullOrWhiteSpace(workingDirectory))
            startInfo.WorkingDirectory = workingDirectory;

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (environmentVariables != null)
        {
            foreach (var pair in environmentVariables)
                startInfo.Environment[pair.Key] = pair.Value;
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                standardOutput.AppendLine(eventArgs.Data);
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data != null)
                standardError.AppendLine(eventArgs.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        return new CommandExecutionResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = standardOutput.ToString().Trim(),
            StandardError = standardError.ToString().Trim()
        };
    }

}
