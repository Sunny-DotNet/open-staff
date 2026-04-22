using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Options;

namespace OpenStaff.EntityFrameworkCore;

/// <summary>
/// 在移除 <c>ProviderAccounts.EnvConfig</c> 列前，把历史配置回填到工作目录文件。
/// Backfills legacy <c>ProviderAccounts.EnvConfig</c> values into working-directory files before the database column is removed.
/// </summary>
public static class ProviderAccountEnvConfigBackfill
{
    /// <summary>
    /// 如果数据库中仍存在旧列，则把未落盘的历史 Provider 配置导出到 <c>providers/{id}.json</c>。
    /// Exports any still-database-backed provider configs into <c>providers/{id}.json</c> when the legacy column is present.
    /// </summary>
    public static async Task BackfillAsync(AppDbContext db, OpenStaffOptions openStaffOptions, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(openStaffOptions);

        if (!await HasLegacyEnvConfigColumnAsync(db, ct))
            return;

        var providerDirectory = Path.Combine(openStaffOptions.WorkingDirectory, "providers");
        await using var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = GetLegacyEnvConfigSelectSql(db);

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0) || reader.IsDBNull(1))
                    continue;

                var accountId = ReadGuid(reader, 0);
                var envConfig = reader.GetString(1);
                if (string.IsNullOrWhiteSpace(envConfig))
                    continue;

                var filePath = Path.Combine(providerDirectory, $"{accountId}.json");
                if (File.Exists(filePath))
                    continue;

                Directory.CreateDirectory(providerDirectory);
                await File.WriteAllTextAsync(filePath, envConfig, ct);
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<bool> HasLegacyEnvConfigColumnAsync(AppDbContext db, CancellationToken ct)
    {
        await using var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = GetLegacyEnvConfigColumnCheckSql(db);
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result) > 0;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string GetLegacyEnvConfigColumnCheckSql(AppDbContext db)
    {
        if (db.Database.IsSqlite())
            return "SELECT COUNT(*) FROM pragma_table_info('ProviderAccounts') WHERE name = 'EnvConfig';";

        if (IsNpgsql(db))
        {
            return """
                SELECT COUNT(*)
                FROM information_schema.columns
                WHERE table_schema = current_schema()
                  AND table_name = 'ProviderAccounts'
                  AND column_name = 'EnvConfig';
                """;
        }

        throw new NotSupportedException($"Provider '{db.Database.ProviderName}' is not supported by the legacy ProviderAccount EnvConfig backfill.");
    }

    private static string GetLegacyEnvConfigSelectSql(AppDbContext db)
    {
        if (db.Database.IsSqlite())
            return "SELECT Id, EnvConfig FROM ProviderAccounts WHERE EnvConfig IS NOT NULL AND EnvConfig <> '';";

        if (IsNpgsql(db))
        {
            return """
                SELECT "Id", "EnvConfig"
                FROM "ProviderAccounts"
                WHERE "EnvConfig" IS NOT NULL
                  AND "EnvConfig" <> '';
                """;
        }

        throw new NotSupportedException($"Provider '{db.Database.ProviderName}' is not supported by the legacy ProviderAccount EnvConfig backfill.");
    }

    private static bool IsNpgsql(AppDbContext db)
        => db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;

    private static Guid ReadGuid(DbDataReader reader, int ordinal)
    {
        var value = reader.GetValue(ordinal);
        return value switch
        {
            Guid guid => guid,
            string text => Guid.Parse(text),
            byte[] bytes when bytes.Length == 16 => new Guid(bytes),
            _ => Guid.Parse(Convert.ToString(value)!),
        };
    }
}
