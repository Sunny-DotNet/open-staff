using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OpenStaff.Agents.Debugger;

/// <summary>
/// 测试运行器 — 执行 dotnet test 并解析结果
/// Test runner — executes dotnet test and parses results
/// </summary>
public class TestRunner
{
    private readonly ILogger _logger;

    public TestRunner(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 运行 .NET 测试 / Run .NET tests
    /// </summary>
    public async Task<TestResult> RunDotnetTestsAsync(string workspacePath, int timeoutSeconds = 120)
    {
        var result = new TestResult();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "test --no-restore --verbosity normal",
                WorkingDirectory = workspacePath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                result.Output = "无法启动 dotnet test 进程 / Failed to start dotnet test process";
                return result;
            }

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = process.WaitForExit(timeoutSeconds * 1000);
            if (!completed)
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                result.Output = "测试超时 / Test timed out";
                return result;
            }

            var output = await outputTask;
            var error = await errorTask;
            result.Output = output + (string.IsNullOrEmpty(error) ? "" : $"\nSTDERR:\n{error}");
            result.Success = process.ExitCode == 0;

            ParseTestOutput(output, result);

            _logger.LogInformation("测试完成: {Passed}/{Total} 通过 / Tests complete: {Passed}/{Total} passed",
                result.PassedTests, result.TotalTests, result.PassedTests, result.TotalTests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "运行测试失败 / Failed to run tests");
            result.Output = $"运行测试异常: {ex.Message} / Test execution error: {ex.Message}";
        }

        return result;
    }

    private static void ParseTestOutput(string output, TestResult result)
    {
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();

            // 解析摘要行: "Passed!  - Failed:     0, Passed:     3, Skipped:     0, Total:     3"
            if (trimmed.Contains("Total:"))
            {
                var parts = trimmed.Split(',');
                foreach (var part in parts)
                {
                    var kv = part.Split(':');
                    if (kv.Length == 2)
                    {
                        var key = kv[0].Trim().TrimStart('-', ' ').Replace("!", "");
                        if (int.TryParse(kv[1].Trim(), out var val))
                        {
                            if (key.Contains("Total")) result.TotalTests = val;
                            else if (key.Contains("Passed")) result.PassedTests = val;
                        }
                    }
                }
            }

            // 识别失败的测试名 / Identify failed test names
            if (trimmed.StartsWith("Failed "))
            {
                result.FailedTests.Add(trimmed);
            }
        }
    }
}

/// <summary>
/// 测试结果 / Test result
/// </summary>
public class TestResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public List<string> FailedTests { get; set; } = new();
}
