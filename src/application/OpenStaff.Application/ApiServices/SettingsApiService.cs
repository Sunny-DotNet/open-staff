using OpenStaff.Application.Settings.Services;

namespace OpenStaff.ApiServices;
/// <summary>
/// 系统设置应用服务实现。
/// Application service implementation for system settings.
/// </summary>
public class SettingsApiService : ApiServiceBase, ISettingsApiService
{
    private readonly SettingsService _settingsService;

    /// <summary>
    /// 初始化系统设置应用服务。
    /// Initializes the system settings application service.
    /// </summary>
    public SettingsApiService(SettingsService settingsService, IServiceProvider? serviceProvider = null)
        : base(serviceProvider)
    {
        _settingsService = settingsService;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAllSettingsAsync(ct);
        return settings.ToDictionary(s => s.Key, s => s.Value ?? string.Empty);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Dictionary<string, string> settings, CancellationToken ct)
    {
        await _settingsService.UpdateSettingsAsync(settings, ct);
    }

    /// <inheritdoc/>
    public async Task<SystemSettingsDto> GetSystemAsync(CancellationToken ct = default)
    {
        var all = await GetAllAsync(ct);
        var dto = new SystemSettingsDto();

        if (all.TryGetValue(SystemSettingsKeys.TeamName, out var teamName) && !string.IsNullOrEmpty(teamName))
            dto.TeamName = teamName;
        if (all.TryGetValue(SystemSettingsKeys.TeamDescription, out var teamDescription) && !string.IsNullOrEmpty(teamDescription))
            dto.TeamDescription = teamDescription;
        if (all.TryGetValue(SystemSettingsKeys.UserName, out var userName) && !string.IsNullOrEmpty(userName))
            dto.UserName = userName;
        if (all.TryGetValue(SystemSettingsKeys.Language, out var lang) && !string.IsNullOrEmpty(lang))
            dto.Language = lang;
        if (all.TryGetValue(SystemSettingsKeys.Timezone, out var tz) && !string.IsNullOrEmpty(tz))
            dto.Timezone = tz;
        if (all.TryGetValue(SystemSettingsKeys.DefaultTemperature, out var temp) && double.TryParse(temp, out var tempVal))
            dto.DefaultTemperature = tempVal;
        if (all.TryGetValue(SystemSettingsKeys.DefaultMaxTokens, out var maxTok) && int.TryParse(maxTok, out var maxTokVal))
            dto.DefaultMaxTokens = maxTokVal;
        if (all.TryGetValue(SystemSettingsKeys.ResponseStyle, out var style) && !string.IsNullOrEmpty(style))
            dto.ResponseStyle = style;
        if (all.TryGetValue(SystemSettingsKeys.ProjectGroupAutoApproveCapabilities, out var autoApprove)
            && bool.TryParse(autoApprove, out var autoApproveEnabled))
        {
            dto.AutoApproveProjectGroupCapabilities = autoApproveEnabled;
        }

        return dto;
    }

    /// <inheritdoc/>
    public async Task UpdateSystemAsync(SystemSettingsDto dto, CancellationToken ct = default)
    {
        var dict = new Dictionary<string, string>
        {
            [SystemSettingsKeys.TeamName] = dto.TeamName,
            [SystemSettingsKeys.TeamDescription] = dto.TeamDescription,
            [SystemSettingsKeys.UserName] = dto.UserName,
            [SystemSettingsKeys.Language] = dto.Language,
            [SystemSettingsKeys.Timezone] = dto.Timezone,
            [SystemSettingsKeys.DefaultTemperature] = dto.DefaultTemperature.ToString("F2"),
            [SystemSettingsKeys.DefaultMaxTokens] = dto.DefaultMaxTokens.ToString(),
            [SystemSettingsKeys.ResponseStyle] = dto.ResponseStyle,
            [SystemSettingsKeys.ProjectGroupAutoApproveCapabilities] = dto.AutoApproveProjectGroupCapabilities.ToString(),
        };
        await UpdateAsync(dict, ct);
    }
}



