using OpenStaff.Application.Contracts.Auth.Dtos;

namespace OpenStaff.Application.Contracts.Auth;

public interface IDeviceAuthAppService
{
    Task<DeviceCodeDto> InitiateAsync(Guid providerAccountId, CancellationToken ct = default);
    Task<DeviceAuthPollDto> PollAsync(Guid providerAccountId, CancellationToken ct = default);
    void Cancel(Guid providerAccountId);
}
