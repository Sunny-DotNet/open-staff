using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenStaff.Core.Modularity;

/// <summary>
/// 服务配置上下文 / Service configuration context passed to modules during <c>ConfigureServices</c>.
/// </summary>
public class ServiceConfigurationContext
{
    /// <summary>服务集合 / Service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>应用配置 / Application configuration.</summary>
    public IConfiguration Configuration { get; }

    /// <summary>创建服务配置上下文 / Create the service configuration context.</summary>
    /// <param name="services">服务集合 / Service collection.</param>
    /// <param name="configuration">应用配置 / Application configuration.</param>
    public ServiceConfigurationContext(IServiceCollection services, IConfiguration configuration)
    {
        Services = services;
        Configuration = configuration;
    }
}
