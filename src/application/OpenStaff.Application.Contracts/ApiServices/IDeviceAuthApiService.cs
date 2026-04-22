using OpenStaff.Dtos;

namespace OpenStaff.ApiServices;
/// <summary>
/// 设备授权流程应用服务契约。
/// Application service contract for device authorization flows.
/// </summary>
public interface IDeviceAuthApiService : IApiServiceBase
{
    /// <summary>
    /// 启动指定账户的设备授权流程。
    /// Starts the device authorization flow for a provider account.
    /// </summary>
    Task<DeviceCodeDto> InitiateAsync(Guid providerAccountId, CancellationToken ct = default);

    /// <summary>
    /// 轮询设备授权结果。
    /// Polls the current status of a device authorization flow.
    /// </summary>
    Task<DeviceAuthPollDto> PollAsync(Guid providerAccountId, CancellationToken ct = default);

    /// <summary>
    /// 取消设备授权轮询。
    /// Cancels device authorization polling.
    /// </summary>
    void Cancel(Guid providerAccountId);
}


