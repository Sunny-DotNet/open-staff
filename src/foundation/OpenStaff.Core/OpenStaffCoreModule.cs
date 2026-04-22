using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Options;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// OpenStaff 核心模块 / Root OpenStaff module with no external dependencies.
/// 提供模块基础设施、接口定义和领域模型。
/// Provides the modular infrastructure, core contracts, and domain models.
/// </summary>
public class OpenStaffCoreModule : OpenStaffModule
{
    /// <summary>
    /// 注册核心默认配置 / Register the core option type so constructor-based defaults are available to the container.
    /// </summary>
    /// <param name="context">服务配置上下文 / Service configuration context.</param>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var workingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".staff");
        Environment.CurrentDirectory = workingDirectory;
        Configure<OpenStaffOptions>(options => {
            options.WorkingDirectory = workingDirectory;
        });
    }
}
