
using Microsoft.EntityFrameworkCore;
using OpenStaff.Application.Contracts.Settings;
using OpenStaff.EntityFrameworkCore;
using OpenStaff.Repositories;

namespace OpenStaff.HttpApi.Host.Middleware;

/// <summary>
/// 语言检测中间件，优先级为数据库配置、请求头、服务器区域设置。
/// Locale detection middleware whose precedence is database setting, request header, then server locale.
/// </summary>
public class LocaleMiddleware
{
    private readonly RequestDelegate _next;
    private string? _dbLocaleCache;
    private DateTime _dbLocaleCacheExpiry;

    /// <summary>
    /// 初始化语言检测中间件。
    /// Initializes the locale detection middleware.
    /// </summary>
    public LocaleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// 解析当前请求的语言并写入 HttpContext.Items。
    /// Resolves the locale for the current request and stores it in HttpContext.Items.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IGlobalSettingRepository globalSettings)
    {
        string? locale = null;

        if (_dbLocaleCache == null || DateTime.UtcNow > _dbLocaleCacheExpiry)
        {
            var setting = await globalSettings.FirstOrDefaultAsync(s =>
                s.Key == SystemSettingsKeys.Language || s.Key == "locale");
            _dbLocaleCache = setting?.Value;
            _dbLocaleCacheExpiry = DateTime.UtcNow.AddMinutes(5);
        }

        if (!string.IsNullOrEmpty(_dbLocaleCache))
            locale = _dbLocaleCache;

        if (string.IsNullOrEmpty(locale))
        {
            locale = context.Request.Headers["Accept-Language"]
                .FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }

        if (string.IsNullOrEmpty(locale))
        {
            locale = System.Globalization.CultureInfo.CurrentCulture.Name;
        }

        if (string.IsNullOrEmpty(locale))
            locale = "zh-CN";

        context.Items["Locale"] = locale;
        await _next(context);
    }
}
