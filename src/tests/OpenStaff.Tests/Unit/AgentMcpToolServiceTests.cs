using OpenStaff.Application.Orchestration.Services;

namespace OpenStaff.Tests.Unit;

public sealed class AgentMcpToolServiceTests
{
    [Fact]
    public void ParseToolFilter_ShouldMapLegacyCmdAliasToShellExec()
    {
        var filter = AgentMcpToolService.ParseToolFilter("""["cmd","read_file"]""");

        Assert.Contains("shell_exec", filter);
        Assert.Contains("shell_system_info", filter);
        Assert.Contains("read_file", filter);
        Assert.DoesNotContain("cmd", filter);
    }

    [Fact]
    public void ParseToolFilter_ShouldGrantSystemInfoWhenShellExecIsAllowed()
    {
        var filter = AgentMcpToolService.ParseToolFilter("""["shell.exec"]""");

        Assert.Contains("shell_exec", filter);
        Assert.Contains("shell_system_info", filter);
    }
}
