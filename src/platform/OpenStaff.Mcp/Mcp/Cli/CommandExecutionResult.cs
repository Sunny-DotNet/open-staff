namespace OpenStaff.Mcp.Cli;

/// <summary>
/// zh-CN: 表示一次命令执行结果。
/// en: Represents the result of a process execution.
/// </summary>
public sealed class CommandExecutionResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public void EnsureSuccess(string commandDescription)
    {
        if (ExitCode == 0)
            return;

        throw new InvalidOperationException(
            $"{commandDescription} failed with exit code {ExitCode}.{Environment.NewLine}{StandardError}{Environment.NewLine}{StandardOutput}".Trim());
    }
}
