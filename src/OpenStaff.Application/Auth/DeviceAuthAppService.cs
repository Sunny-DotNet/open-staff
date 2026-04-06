using OpenStaff.Application.Contracts.Auth;
using OpenStaff.Application.Contracts.Auth.Dtos;
using OpenStaff.Application.Providers;

namespace OpenStaff.Application.Auth;

public class DeviceAuthAppService : IDeviceAuthAppService
{
    private readonly GitHubDeviceAuthService _deviceAuthService;
    private readonly ProviderAccountService _accountService;

    public DeviceAuthAppService(GitHubDeviceAuthService deviceAuthService, ProviderAccountService accountService)
    {
        _deviceAuthService = deviceAuthService;
        _accountService = accountService;
    }

    public async Task<DeviceCodeDto> InitiateAsync(Guid providerAccountId, CancellationToken ct)
    {
        var account = await _accountService.GetByIdAsync(providerAccountId);
        if (account == null) throw new KeyNotFoundException($"Provider account {providerAccountId} not found");
        if (account.ProtocolType != "github-copilot")
            throw new InvalidOperationException("仅 GitHub Copilot 支持设备码授权");

        var result = await _deviceAuthService.InitiateAsync(providerAccountId, ct);
        return new DeviceCodeDto
        {
            UserCode = result.UserCode,
            VerificationUri = result.VerificationUri,
            ExpiresIn = result.ExpiresIn,
            Interval = result.Interval
        };
    }

    public async Task<DeviceAuthPollDto> PollAsync(Guid providerAccountId, CancellationToken ct)
    {
        var result = await _deviceAuthService.PollAsync(providerAccountId, ct);
        return new DeviceAuthPollDto
        {
            Status = result.Status,
            Message = result.Message,
            Interval = result.Interval
        };
    }

    public void Cancel(Guid providerAccountId)
    {
        _deviceAuthService.Cancel(providerAccountId);
    }
}
