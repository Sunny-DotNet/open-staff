using OpenStaff.Entities;
using OpenStaff.Mcp;

namespace OpenStaff.Tests.Unit;

public class McpStructuredMetadataFactoryTests
{
    [Fact]
    public void Build_ForStructuredTemplate_ProducesProfilesAndSchema()
    {
        var factory = new McpStructuredMetadataFactory();

        var metadata = factory.Build(new McpServer
        {
            Name = "Filesystem",
            Icon = "folder",
            DefaultConfig =
                """
                {
                  "schema": "openstaff.mcp-template.v1",
                  "logo": "Folder",
                  "default_profile_id": "package-npm",
                  "profiles": [
                    {
                      "id": "package-npm",
                      "display_name": "NPM",
                      "profile_type": "package",
                      "transport_type": "stdio",
                      "runner_kind": "package",
                      "runner": "npx",
                      "ecosystem": "npm",
                      "package_name": "@modelcontextprotocol/server-filesystem",
                      "args_template": ["-y", "@modelcontextprotocol/server-filesystem", "{workspace}"]
                    }
                  ],
                  "parameter_schema": [
                    {
                      "key": "workspacePath",
                      "label": "Workspace Path",
                      "type": "string",
                      "default_value": "${project.workspace}",
                      "description": "Working directory."
                    }
                  ]
                }
                """
        });

        Assert.Equal("Folder", metadata.Logo);
        Assert.Equal("package-npm", metadata.DefaultProfileId);
        var profile = Assert.Single(metadata.Profiles);
        Assert.Equal("package", profile.ProfileType);
        Assert.Equal("npx", profile.Runner);
        Assert.Equal("@modelcontextprotocol/server-filesystem", profile.PackageName);

        var schema = Assert.Single(metadata.ParameterSchema);
        Assert.Equal("workspacePath", schema.Key);
        Assert.Null(schema.DefaultValueSource);
        Assert.Null(schema.DefaultValue);
        Assert.Equal("project-workspace", schema.ProjectOverrideValueSource);
    }

    [Fact]
    public void Build_ForNonTemplateServer_DoesNotInferLegacyMetadata()
    {
        var factory = new McpStructuredMetadataFactory();

        var metadata = factory.Build(new McpServer
        {
            Name = "GitHub",
            NpmPackage = "@modelcontextprotocol/server-github",
            TransportType = McpTransportTypes.Stdio,
            DefaultConfig = """{"command":"npx","args":["-y","@modelcontextprotocol/server-github"],"env":{"GITHUB_PERSONAL_ACCESS_TOKEN":""}}"""
        });

        Assert.Empty(metadata.Profiles);
        Assert.Empty(metadata.ParameterSchema);
    }
}
