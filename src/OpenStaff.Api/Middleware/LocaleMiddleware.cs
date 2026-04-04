using Microsoft.EntityFrameworkCore;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Api.Middleware;

/// <summary>
/// 语言检测中间件 / Locale detection middleware
/// 优先级: 数据库配置 > UI Header > 服务本地 locale
/// Priority: DB config > UI Header > Server locale
/// </summary>
public class LocaleMiddleware
{
    private readonly RequestDelegate _next;
    private string? _dbLocaleCache;
    private DateTime _dbLocaleCacheExpiry;

    public LocaleMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        string? locale = null;

        // 1. 最高优先级：数据库配置 / Highest priority: DB config
        if (_dbLocaleCache == null || DateTime.UtcNow > _dbLocaleCacheExpiry)
        {
            var setting = await db.GlobalSettings
                .FirstOrDefaultAsync(s => s.Key == "locale");
            _dbLocaleCache = setting?.Value;
            _dbLocaleCacheExpiry = DateTime.UtcNow.AddMinutes(5); // 缓存5分钟
        }
        if (!string.IsNullOrEmpty(_dbLocaleCache))
            locale = _dbLocaleCache;

        // 2. 中优先级：请求头 Accept-Language / Medium priority: UI header
        if (string.IsNullOrEmpty(locale))
        {
            locale = context.Request.Headers["Accept-Language"]
                .FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim();
        }

        // 3. 最低优先级：服务器默认 / Lowest priority: server locale
        if (string.IsNullOrEmpty(locale))
        {
            locale = System.Globalization.CultureInfo.CurrentCulture.Name;
        }

        // 回退到中文 / Fallback to Chinese
        if (string.IsNullOrEmpty(locale))
            locale = "zh-CN";

        context.Items["Locale"] = locale;
        await _next(context);
    }
}
