using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenStaff.Plugins.ModelDataSource;
using OpenStaff.Provider.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider.Protocols;

public interface IProtocol { 
    bool IsVendor { get; }
    string ProviderName { get; }
    Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);
}
public abstract class ProtocolBase : IProtocol
{
    public abstract bool IsVendor { get; }
    public abstract string ProviderName { get; }
    protected IServiceProvider ServiceProvider { get; }
    protected ILogger Logger { get; }
    protected IModelDataSource ModelDataSource { get; }

    protected ProtocolBase(IServiceProvider serviceProvider) {
        ServiceProvider = serviceProvider;
        Logger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());
        ModelDataSource = serviceProvider.GetRequiredService<IModelDataSource>();
    }

    public abstract Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default);
}

public abstract class VendorProtocolBase(IServiceProvider serviceProvider) : ProtocolBase(serviceProvider)
{
    public override bool IsVendor => true;
    public abstract ModelProtocolType ProtocolType { get; }

    public override async Task<IEnumerable<ModelInfo>> ModelsAsync(CancellationToken cancellationToken = default)
    {
        // 默认实现：从 models.dev 数据源获取对应供应商的模型列表
        var vendorId = ProviderName; // 假设 ProviderName 与 models.dev 中的 vendorId 一致
        var models = await ModelDataSource.GetModelsByVendorAsync(vendorId, cancellationToken);        
        return [.. models.Where(x=>x.InputModalities.HasFlag(ModelModality.Text)&&x.OutputModalities.HasFlag(ModelModality.Text)&&x.Capabilities.HasFlag(ModelCapability.FunctionCall)).Select(MapTo)];
    }

    private ModelInfo MapTo(ModelData source)
    {
        return new ModelInfo(
            ModelSlug: source.Id,
            VenderSlug: source.VendorId,
            ModelProtocols: ProtocolType
        );
    }
}
