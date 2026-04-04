using OpenStaff.Core.Models;

namespace OpenStaff.Core.Services;

/// <summary>
/// 模型客户端工厂接口 / Model client factory
/// </summary>
public interface IModelClientFactory
{
    /// <summary>
    /// 根据供应商配置创建客户端 / Create client from provider config
    /// </summary>
    IModelClient CreateClient(ModelProvider provider);
}
