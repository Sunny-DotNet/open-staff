using OpenStaff.Application.Auth.Services;

namespace OpenStaff.ApiServices;
/// <summary>
/// 设备授权应用服务实现。
/// Application service implementation for device authorization flows.
/// </summary>
public class DeviceAuthApiService : ApiServiceBase, IDeviceAuthApiService
{
    private readonly GitHubDeviceAuthService _deviceAuthService;
    private readonly ProviderAccountService _accountService;

    /// <summary>
    /// Initializes the scoped application service that coordinates device authorization requests with provider-account validation.
    /// 初始化协调设备授权请求与提供商账户校验的 Scoped 应用服务。
    /// </summary>
    /// <param name="deviceAuthService">Underlying GitHub device-flow service that owns polling state. / 持有轮询状态的底层 GitHub 设备流服务。</param>
    /// <param name="accountService">Provider-account store used to validate and load the target account. / 用于校验并加载目标账户的提供商账户存储服务。</param>
    public DeviceAuthApiService(GitHubDeviceAuthService deviceAuthService, ProviderAccountService accountService, IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _deviceAuthService = deviceAuthService;
        _accountService = accountService;
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void Cancel(Guid providerAccountId)
    {
        _deviceAuthService.Cancel(providerAccountId);
    }
}




