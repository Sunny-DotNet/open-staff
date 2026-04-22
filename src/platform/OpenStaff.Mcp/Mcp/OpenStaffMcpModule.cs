using OpenStaff.Application.Contracts;
using OpenStaff.Core.Modularity;
using OpenStaff.Mcp.Cli;
using OpenStaff.Mcp.PackageManagers;
using OpenStaff.Mcp.Persistence;
using OpenStaff.Mcp.Services;
using OpenStaff.Mcp.Sources;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Mcp;

/// <summary>
/// MCP module. This project will absorb MCP definitions, bindings, runtime orchestration, and marketplace sources.
/// </summary>
[DependsOn(typeof(OpenStaffApplicationContractsModule))]
public class OpenStaffMcpModule : OpenStaffModule
{
    /// <summary>
    /// zh-CN: 注册 OpenStaff.Mcp 自己的闭环服务，使宿主只需要引用该模块即可获得搜索、安装、卸载和运行时解析能力。
    /// en: Registers the self-contained OpenStaff.Mcp services so hosts can gain catalog, install, uninstall, and runtime-resolution capabilities by referencing this module only.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<OpenStaffMcpOptions>(OpenStaffMcpOptions.SectionName);

        context.Services.AddHttpClient();

        context.Services.AddSingleton<IMcpCatalogSource, StaticTemplateCatalogSource>();
        context.Services.AddSingleton<IMcpDataDirectoryLayout, McpDataDirectoryLayout>();
        context.Services.AddSingleton<IInstalledMcpMetadataStore, FileInstalledMcpMetadataStore>();
        context.Services.AddSingleton<IMcpManifestStore, FileMcpManifestStore>();
        context.Services.AddSingleton<IInstallLockManager, FileInstallLockManager>();
        context.Services.AddSingleton<IArtifactDownloader, HttpClientArtifactDownloader>();
        context.Services.AddSingleton<IZipExtractor, ZipArchiveExtractor>();
        context.Services.AddSingleton<ICommandRunner, ProcessCommandRunner>();

        context.Services.AddSingleton<IInstallChannelInstaller, NpmInstallChannelInstaller>();
        context.Services.AddSingleton<IInstallChannelInstaller, PyPiInstallChannelInstaller>();
        context.Services.AddSingleton<IInstallChannelInstaller, ZipInstallChannelInstaller>();

        context.Services.AddSingleton<IMcpCatalogService, McpCatalogService>();
        context.Services.AddSingleton<McpStructuredMetadataFactory>();
        context.Services.AddSingleton<McpProfileConnectionRenderer>();
        context.Services.AddSingleton<McpHub>();
        context.Services.AddSingleton<IMcpClientFactory, McpClientFactory>();
        context.Services.AddSingleton<IInstalledMcpService, InstalledMcpService>();
        context.Services.AddSingleton<IMcpInstallationService, McpInstallationService>();
        context.Services.AddSingleton<IMcpRuntimeResolver, McpRuntimeResolver>();
        context.Services.AddSingleton<IMcpUninstallService, McpUninstallService>();
        context.Services.AddSingleton<IMcpRepairService, McpRepairService>();
    }
}
