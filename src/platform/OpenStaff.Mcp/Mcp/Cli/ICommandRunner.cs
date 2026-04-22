namespace OpenStaff.Mcp.Cli;

/// <summary>
/// zh-CN: 命令执行抽象。
/// en: Process-execution abstraction.
/// </summary>
public interface ICommandRunner
{
    Task<CommandExecutionResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}
