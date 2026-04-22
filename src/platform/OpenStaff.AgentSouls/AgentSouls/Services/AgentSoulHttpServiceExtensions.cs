using OpenStaff.AgentSouls.Dtos;

namespace OpenStaff.AgentSouls.Services;

/// <summary>
/// Helper extensions for resolving soul values from canonical keys or localized aliases.
/// </summary>
public static class AgentSoulHttpServiceExtensions
{
    /// <summary>
    /// Finds the canonical key for a soul value, accepting either the key itself or any localized alias.
    /// </summary>
    public static async Task<string?> FindKeyAsync(this IAgentSoulHttpService service, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        var items = await service.GetAllAsync();

        foreach (var item in items)
        {
            if (Matches(item, normalized))
                return item.Key;
        }

        return null;
    }

    /// <summary>
    /// Resolves a soul value into the alias of the requested locale, accepting either the key itself or any localized alias.
    /// </summary>
    public static async Task<string?> ResolveAliasAsync(this IAgentSoulHttpService service, string? value, string? locale = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        var key = await service.FindKeyAsync(normalized);
        if (key is null)
            return normalized;

        return await service.GetAsync(key, locale);
    }

    private static bool Matches(AgentSoulValue item, string value)
    {
        if (string.Equals(item.Key, value, StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var alias in item.Aliases.Values)
        {
            if (string.Equals(alias, value, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
