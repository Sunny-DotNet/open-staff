using Microsoft.EntityFrameworkCore;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Services;

/// <summary>
/// 全局设置服务 / Global settings service
/// </summary>
public class SettingsService
{
    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db)
    {
        _db = db;
    }

    // 全局设置 / Global Settings
    public async Task<List<GlobalSetting>> GetAllSettingsAsync(CancellationToken ct)
    {
        return await _db.GlobalSettings.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync(ct);
    }

    public async Task UpdateSettingsAsync(Dictionary<string, string> settings, CancellationToken ct)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _db.GlobalSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.GlobalSettings.Add(new GlobalSetting { Key = key, Value = value });
            }
        }
        await _db.SaveChangesAsync(ct);
    }
}
