using OpenStaff.Provider.Models;

namespace OpenStaff.Provider.Protocols;

/// <summary>
/// NewApi/OneAPI 兼容协议 — OpenAI 兼容的 API 网关
/// 非供应商协议，模型列表由用户在网关侧配置，此处通过 API 动态获取
/// </summary>
internal class NewApiProtocol(IServiceProvider serviceProvider) : ProtocolBase(serviceProvider)
{
    public override bool IsVendor => false;

    public override string ProviderName => "newapi";

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        // NewApi/OneAPI 网关的模型列表需通过其 /v1/models 端点动态获取
        // 后续实现：根据用户配置的 API_BASE_URL 调用网关的模型列表接口
        await Task.CompletedTask;
        return [];
    }
}
