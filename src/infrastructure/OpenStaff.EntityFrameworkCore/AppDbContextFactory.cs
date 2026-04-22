using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenStaff.EntityFrameworkCore;

/// <summary>
/// 为 EF Core 设计时工具创建数据库上下文。
/// Creates the database context for EF Core design-time tooling.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    /// <summary>
    /// 为 <c>dotnet ef</c> 命令创建数据库上下文。
    /// Creates the database context for <c>dotnet ef</c> commands.
    /// </summary>
    /// <param name="args">命令行参数。/ Command-line arguments.</param>
    /// <returns>设计时数据库上下文。/ The design-time database context.</returns>
    public AppDbContext CreateDbContext(string[] args)
    {
        // zh-CN: 设计时默认值与运行时保持一致，避免迁移命令意外指向另一个本地数据库文件。
        // en: Keep the design-time default aligned with runtime so migration commands do not accidentally target a different local database file.
        var defaultDbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".staff", "openstaff.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={defaultDbPath}")
            .Options;

        return new AppDbContext(options);
    }
}
