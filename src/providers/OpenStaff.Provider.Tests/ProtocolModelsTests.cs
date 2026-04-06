using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenStaff.Core.Modularity;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Protocols;

namespace OpenStaff.Provider.Tests;

/// <summary>
/// 对所有注册的 IProtocol 执行 ModelsAsync 测试，
/// 验证 Vendor 协议能从 models.dev 数据源返回模型列表（长度 > 0）。
/// </summary>
public class ProtocolModelsTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IProtocolFactory _protocolFactory = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddOpenStaffModules<ProviderTestModule>(configuration);
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
        _serviceProvider.UseOpenStaffModules();

        // 初始化 models.dev 数据源
        var dataSource = _serviceProvider.GetRequiredService<IModelDataSource>();
        await dataSource.InitializeAsync();

        _protocolFactory = _serviceProvider.GetRequiredService<IProtocolFactory>();
    }

    public Task DisposeAsync()
    {
        _serviceProvider?.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 确保所有已注册的协议都能被实例化
    /// </summary>
    [Fact]
    public void AllProtocols_ShouldBeCreated()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        Assert.NotEmpty(protocols);
    }

    /// <summary>
    /// 所有 Vendor 协议（OpenAI、Anthropic、Google）的 ModelsAsync 返回值长度 > 0
    /// </summary>
    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("google")]
    public async Task VendorProtocol_ModelsAsync_ShouldReturnModels(string providerKey)
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var protocol = protocols.FirstOrDefault(p =>
            string.Equals(p.ProviderKey, providerKey, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(protocol);
        Assert.True(protocol.IsVendor, $"{providerKey} 应为 Vendor 协议");

        var models = (await protocol.ModelsAsync()).ToList();
        Assert.True(models.Count > 0, $"{providerKey} 的 ModelsAsync 应返回至少 1 个模型，实际返回 {models.Count}");
    }

    /// <summary>
    /// GitHub Copilot 协议存在但无需真实认证，不测试模型数量
    /// </summary>
    [Fact]
    public void GitHubCopilotProtocol_ShouldExist()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var copilot = protocols.FirstOrDefault(p =>
            string.Equals(p.ProviderKey, "github-copilot", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(copilot);
        Assert.False(copilot.IsVendor);
    }

    /// <summary>
    /// NewApi 协议存在但需要配置 BaseUrl，未配置时返回空列表
    /// </summary>
    [Fact]
    public async Task NewApiProtocol_WithoutConfig_ShouldReturnEmpty()
    {
        var protocols = _protocolFactory.AllProtocols().ToList();
        var newApi = protocols.FirstOrDefault(p =>
            string.Equals(p.ProviderKey, "newapi", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(newApi);
        Assert.False(newApi.IsVendor);

        // 未初始化 Env 时，NewApi 的 BaseUrl 为空，应返回空列表
        var models = (await newApi.ModelsAsync()).ToList();
        Assert.Empty(models);
    }

    /// <summary>
    /// 协议元数据应包含所有注册的协议
    /// </summary>
    [Fact]
    public void GetProtocolMetadata_ShouldReturnAllRegistered()
    {
        var metadata = _protocolFactory.GetProtocolMetadata();
        Assert.True(metadata.Count >= 5, $"应至少注册 5 个协议，实际 {metadata.Count}");

        var names = metadata.Select(m => m.ProviderKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("openai", names);
        Assert.Contains("anthropic", names);
        Assert.Contains("google", names);
        Assert.Contains("newapi", names);
        Assert.Contains("github-copilot", names);
    }

    /// <summary>
    /// 每个协议的 EnvSchema 应包含配置字段
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
