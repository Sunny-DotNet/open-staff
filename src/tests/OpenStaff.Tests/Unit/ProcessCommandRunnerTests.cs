using OpenStaff.Mcp.Cli;

namespace OpenStaff.Tests.Unit;

public class ProcessCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_ShouldResolveBatchCommandsFromPathOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var tempRoot = CreateTempRoot();
        var toolsDirectory = Path.Combine(tempRoot, "tools dir");
        var workDirectory = Path.Combine(tempRoot, "work dir");
        Directory.CreateDirectory(toolsDirectory);
        Directory.CreateDirectory(workDirectory);

        try
        {
            var scriptPath = Path.Combine(toolsDirectory, "fake-tool.cmd");
            await File.WriteAllTextAsync(Path.Combine(toolsDirectory, "fake-tool"), "not a windows executable");
            await File.WriteAllTextAsync(
                scriptPath,
                """
                @echo off
                echo arg=%~1
                echo cwd=%CD%
                """);

            var runner = new ProcessCommandRunner();
            var result = await runner.RunAsync(
                "fake-tool",
                ["hello there"],
                workDirectory,
                new Dictionary<string, string?>
                {
                    ["PATH"] = toolsDirectory,
                    ["PATHEXT"] = ".CMD"
                });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("arg=hello there", result.StandardOutput);
            Assert.Contains($"cwd={workDirectory}", result.StandardOutput);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private static string CreateTempRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "openstaff-process-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
