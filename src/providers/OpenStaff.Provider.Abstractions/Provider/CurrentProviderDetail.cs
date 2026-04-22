using OpenHub.Agents;
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenStaff.Provider;

public interface ICurrentProviderDetail
{
    ProviderDetail? Current { get; }

    IDisposable Use(ProviderDetail providerDetail);
}

internal class CurrentProviderDetail : ICurrentProviderDetail
{
    private static AsyncLocal<ProviderDetail?> AsyncLocal { get; } = new();
    public ProviderDetail? Current => AsyncLocal.Value;
    public IDisposable Use(ProviderDetail providerDetail)
    {
        var old = Current;
        AsyncLocal.Value = providerDetail;
        return DisposeAction.Create(() => AsyncLocal.Value = old);
    }
}
public class ProviderDetail {
    public string AccountId { get; }
    public ProviderDetail(string accountId)
    {
        AccountId = accountId;
    }
}