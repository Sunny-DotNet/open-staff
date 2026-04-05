using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 服务配置上下文，在 ConfigureServices 阶段传递给各模块。
/// </summary>
public class ServiceConfigurationContext
{
    public IServiceCollection Services { get; }
    public IConfiguration Configuration { get; }

    public ServiceConfigurationContext(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }
}
