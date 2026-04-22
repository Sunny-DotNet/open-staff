using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Skills.Services;
using OpenStaff.Skills.Sources;

namespace OpenStaff;

/// <summary>
/// Skill catalog module backed by skills.sh sources and GitHub archive helpers.
/// </summary>
[DependsOn(typeof(OpenStaffCoreModule))]
public class OpenStaffSkillsModule : OpenStaffModule
{
    /// <summary>
    /// Registers lightweight skill catalog services and archive access helpers.
    /// </summary>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClient(OpenStaffSkillsDefaults.CatalogHttpClientName, client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenStaff");
        });
        context.Services.AddHttpClient(OpenStaffSkillsDefaults.GitHubHttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.github.com/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("OpenStaff");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        });
        context.Services.AddSingleton<IGitHubSkillArchiveClient, GitHubSkillArchiveClient>();
        context.Services.AddSingleton<ISkillCatalogSource, SkillsShCatalogSource>();
        context.Services.AddSingleton<ISkillCatalogService, SkillCatalogService>();
    }
}

internal static class OpenStaffSkillsDefaults
{
    public const string CatalogHttpClientName = "OpenStaff.Skills.Catalog";
    public const string GitHubHttpClientName = "OpenStaff.Skills.GitHub";
}
