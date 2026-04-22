using System.Text.Json.Nodes;
using OpenStaff.Entities;
using OpenStaff.Mcp;

namespace OpenStaff.Tests.Unit;

public class McpProfileConnectionRendererTests
{
    [Fact]
    public void RenderForConfig_ManagedTemplateServer_PreservesInstalledRuntimeCommand()
    {
        var renderer = new McpProfileConnectionRenderer(new McpStructuredMetadataFactory());
        var server = new McpServer
        {
            Name = "Playwright",
            TransportType = McpTransportTypes.Stdio,
            DefaultConfig =
                """
                {
                  "schema": "openstaff.mcp-template.v1",
                  "default_profile_id": "package-npm",
                  "profiles": [
                    {
                      "id": "package-npm",
                      "profile_type": "package",
                      "transport_type": "stdio",
                      "runner_kind": "package",
                      "runner": "npx",
                      "ecosystem": "npm",
                      "package_name": "@playwright/mcp",
                      "package_version": "latest",
                      "command": "npx",
                      "args_template": ["-y", "@playwright/mcp@latest"]
                    }
                  ],
                  "parameter_schema": []
                }
                """,
            InstallInfo =
                """
                {
                  "installId": "de056553-28f6-476f-984a-c86a949d1d22",
                  "catalogEntryId": "official.playwright.current",
                  "sourceKey": "mcps",
                  "channelId": "package-npm",
                  "channelType": "npm",
                  "transportType": "stdio",
                  "command": "C:\\Program Files\\nodejs\\node.exe",
                  "args": ["node_modules\\@playwright\\mcp\\cli.js"],
                  "workingDirectory": "C:\\Users\\Administrator\\AppData\\Local\\OpenStaff\\mcp\\installs\\npm\\@playwright\\mcp\\0.0.70"
                }
                """
        };

        var rendered = renderer.RenderForConfig(server, "package-npm", null);

        Assert.Equal("stdio", rendered["transportType"]?.GetValue<string>());
        Assert.Equal(@"C:\Program Files\nodejs\node.exe", rendered["command"]?.GetValue<string>());

        var args = Assert.IsType<JsonArray>(rendered["args"]);
        Assert.Equal(@"node_modules\@playwright\mcp\cli.js", Assert.Single(args).GetValue<string>());
        Assert.Equal(
            @"C:\Users\Administrator\AppData\Local\OpenStaff\mcp\installs\npm\@playwright\mcp\0.0.70",
            rendered["workingDirectory"]?.GetValue<string>());
    }
}
