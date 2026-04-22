using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Models;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider.Tests;

/// <summary>
/// zh-CN: 覆盖协议工厂的集成式模型发现行为，重点验证注册协议是否能暴露预期元数据与模型列表。
/// en: Covers the protocol factory's integrated model-discovery behavior, focusing on whether registered protocols expose the expected metadata and model lists.
/// </summary>
public class ProtocolModelsTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IProtocolFactory _protocolFactory = null!;

    /// <summary>
    /// zh-CN: 初始化测试容器并预热 models.dev 数据源，让协议测试在接近真实启动流程的环境中运行。
    /// en: Initializes the test container and warms the models.dev data source so protocol tests run under a near-real startup flow.
    /// </summary>
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddOpenStaffModules<ProviderTestModule>(configuration);
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UseOpenStaffModules();

        // zh-CN: 预热 models.dev 数据源，确保 Vendor 协议命中已初始化的模型缓存。
        // en: Warm the models.dev source so vendor protocols hit initialized model data.
        var dataSource = _serviceProvider.GetRequiredService<IModelDataSource>();
        await dataSource.InitializeAsync();

        _protocolFactory = _serviceProvider.GetRequiredService<IProtocolFactory>();
    }

    /// <summary>
    /// zh-CN: 释放测试服务容器，避免协议模块与缓存状态影响后续用例。
    /// en: Disposes the test service container so protocol modules and cached state do not affect later tests.
    /// </summary>
    public Task DisposeAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
            return asyncDisposable.DisposeAsync().AsTask();

        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// zh-CN: 确保所有已注册协议都能被工厂枚举并成功实例化。
    /// en: Ensures every registered protocol can be enumerated and instantiated by the factory.
    /// </summary>
    [Fact]
    public void AllProtocols_ShouldBeCreated()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        Assert.NotEmpty(protocols);
    }

    /// <summary>
    /// zh-CN: 验证 Vendor 协议会从共享模型数据源返回非空模型列表，而不是空配置。
    /// en: Verifies vendor protocols return a non-empty model list from the shared model data source instead of behaving like empty configurations.
    /// </summary>
    [Theory]
    [InlineData("openai")]
    [InlineData("google")]
    public async Task VendorProtocol_ModelsAsync_ShouldReturnModels(string providerKey)
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var protocol = protocols.FirstOrDefault(p =>
            string.Equals(p.ProtocolKey, providerKey, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(protocol);
        Assert.True(protocol.IsVendor, $"{providerKey} 应为 Vendor 协议");

        var models = (await protocol.ModelsAsync()).ToList();
        Assert.True(models.Count > 0, $"{providerKey} 的 ModelsAsync 应返回至少 1 个模型，实际返回 {models.Count}");
        Assert.All(models, model => Assert.NotEqual(ModelProtocolType.None, model.ModelProtocols));
    }

    /// <summary>
    /// zh-CN: 验证 GitHub Copilot 协议已注册且被标记为非 Vendor，避免要求真实认证即可通过。
    /// en: Verifies the GitHub Copilot protocol is registered and marked as non-vendor so the test does not require live authentication.
    /// </summary>
    [Fact]
    public void GitHubCopilotProtocol_ShouldExist()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var copilot = protocols.FirstOrDefault(p =>
            string.Equals(p.ProtocolKey, "github-copilot", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(copilot);
        Assert.False(copilot.IsVendor);
    }

    /// <summary>
    /// zh-CN: 验证 NewApi 协议在缺少 BaseUrl 配置时安全返回空模型列表，而不是抛出误导性错误。
    /// en: Verifies the NewApi protocol safely returns an empty model list without a BaseUrl instead of throwing a misleading error.
    /// </summary>
    [Fact]
    public async Task NewApiProtocol_WithoutConfig_ShouldReturnEmpty()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var newApi = protocols.FirstOrDefault(p =>
            string.Equals(p.ProtocolKey, "newapi", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(newApi);
        Assert.False(newApi.IsVendor);

        // zh-CN: 缺少 Env 配置时，NewApi 没有 BaseUrl，应安全返回空列表。
        // en: Without environment configuration, NewApi has no BaseUrl and should safely return an empty list.
        var models = (await newApi.ModelsAsync()).ToList();
        Assert.Empty(models);
    }

    /// <summary>
    /// zh-CN: 验证协议元数据快照包含所有关键注册协议，保护发现页和配置页所依赖的目录信息。
    /// en: Verifies the protocol metadata snapshot includes all key registered protocols relied on by discovery and configuration screens.
    /// </summary>
    [Fact]
    public void GetProtocolMetadata_ShouldReturnAllRegistered()
    {
        var metadata = _protocolFactory.GetProtocolMetadata();
        Assert.True(metadata.Count >= 5, $"应至少注册 5 个协议，实际 {metadata.Count}");

        var names = metadata.Select(m => m.ProtocolKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("openai", names);
        Assert.Contains("anthropic", names);
        Assert.Contains("google", names);
        Assert.Contains("newapi", names);
        Assert.Contains("github-copilot", names);
    }

    /// <summary>
    /// zh-CN: 验证每个协议都暴露配置字段定义，避免前端或配置工具遇到空的环境变量架构。
    /// en: Verifies every protocol exposes configuration field definitions so UI and tooling do not receive an empty environment schema.
    /// </summary>
    [Fact]
    public void AllProtocols_ShouldHaveEnvSchema()
    {
        var metadata = _protocolFactory.GetProtocolMetadata();

        foreach (var meta in metadata)
        {
            Assert.NotEmpty(meta.EnvSchema);
        }
    }
}
