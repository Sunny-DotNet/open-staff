namespace OpenStaff.Application.Contracts.Settings;

public interface ISettingsAppService
{
    Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct = default);
    Task UpdateAsync(Dictionary<string, string> settings, CancellationToken ct = default);
}
