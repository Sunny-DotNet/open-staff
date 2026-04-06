using OpenStaff.Application.Contracts.Settings;
using OpenStaff.Core.Models;

namespace OpenStaff.Application.Settings;

public class SettingsAppService : ISettingsAppService
{
    private readonly SettingsService _settingsService;

    public SettingsAppService(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetAllSettingsAsync(ct);
        return settings.ToDictionary(s => s.Key, s => s.Value ?? string.Empty);
    }

    public async Task UpdateAsync(Dictionary<string, string> settings, CancellationToken ct)
    {
        await _settingsService.UpdateSettingsAsync(settings, ct);
    }
}
