using Microsoft.EntityFrameworkCore;
using OpenStaff.Entities;
using OpenStaff.Repositories;

namespace OpenStaff.Application.Settings.Services;
/// <summary>
/// 全局设置存储服务。
/// Storage service for raw global settings.
/// </summary>
public class SettingsService
{
    private readonly IGlobalSettingRepository _globalSettings;
    private readonly IRepositoryContext _repositoryContext;

    /// <summary>
    /// 初始化全局设置存储服务。
    /// Initializes the raw global-settings storage service.
    /// </summary>
    public SettingsService(IGlobalSettingRepository globalSettings, IRepositoryContext repositoryContext)
    {
        _globalSettings = globalSettings;
        _repositoryContext = repositoryContext;
    }

    /// <summary>
    /// 获取所有全局设置。
    /// Gets all global settings.
    /// </summary>
    public async Task<List<GlobalSetting>> GetAllSettingsAsync(CancellationToken ct)
    {
        return await _globalSettings.OrderBy(s => s.Category).ThenBy(s => s.Key).ToListAsync(ct);
    }

    /// <summary>
    /// 批量更新全局设置。
    /// Updates global settings in a batch.
    /// </summary>
    public async Task UpdateSettingsAsync(Dictionary<string, string> settings, CancellationToken ct)
    {
        foreach (var (key, value) in settings)
        {
            var existing = await _globalSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
            if (existing != null)
            {
                existing.Value = value;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _globalSettings.Add(new GlobalSetting { Key = key, Value = value });
            }
        }
        await _repositoryContext.SaveChangesAsync(ct);
    }
}

