using System.Text.RegularExpressions;

namespace OpenStaff.Core.Agents;

public static partial class AgentJobTitleCatalog
{
    public const string SecretaryKey = "secretary";

    private sealed record JobTitleDefinition(
        string Key,
        string EnglishName,
        string ChineseName,
        params string[] Aliases);

    private static readonly IReadOnlyList<JobTitleDefinition> Definitions =
    [
        new(SecretaryKey, "Secretary", "秘书", "项目秘书", "Secretary"),
        new("architect", "Architect", "架构师", "Architect"),
        new("producer", "Software Engineer", "开发工程师", "Producer"),
        new("builder", "Builder", "构建工程师", "Builder"),
        new("software_engineer", "Software Engineer", "软件工程师", "Software Engineer"),
        new("backend_engineer", "Backend Engineer", "后端工程师", "Backend Engineer"),
        new("code_reviewer", "Code Reviewer", "代码审查员", "Code Reviewer"),
        new("designer", "Designer", "美工", "Designer"),
    ];

    private static readonly IReadOnlyDictionary<string, JobTitleDefinition> DefinitionsByKey =
        Definitions.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> AliasToKey =
        BuildAliasDictionary();

    public static string? NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (AliasToKey.TryGetValue(trimmed, out var aliasedKey))
            return aliasedKey;

        var candidate = NormalizeKeyCandidate(trimmed);
        if (AliasToKey.TryGetValue(candidate, out aliasedKey))
            return aliasedKey;

        return IsKeyCandidate(candidate) ? candidate : trimmed;
    }

    public static string? Localize(string? value, string? locale)
    {
        var normalizedKey = NormalizeKey(value);
        if (normalizedKey is null)
            return null;

        if (DefinitionsByKey.TryGetValue(normalizedKey, out var definition))
            return IsChineseLocale(locale) ? definition.ChineseName : definition.EnglishName;

        return HumanizeKey(normalizedKey);
    }

    public static string? ToEnglish(string? value)
    {
        var normalizedKey = NormalizeKey(value);
        if (normalizedKey is null)
            return null;

        return DefinitionsByKey.TryGetValue(normalizedKey, out var definition)
            ? definition.EnglishName
            : HumanizeKey(normalizedKey);
    }

    public static bool IsSecretary(string? value)
        => string.Equals(NormalizeKey(value), SecretaryKey, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyDictionary<string, string> BuildAliasDictionary()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Definitions)
        {
            map[definition.Key] = definition.Key;
            map[NormalizeKeyCandidate(definition.Key)] = definition.Key;
            map[definition.EnglishName] = definition.Key;
            map[NormalizeKeyCandidate(definition.EnglishName)] = definition.Key;
            map[definition.ChineseName] = definition.Key;

            foreach (var alias in definition.Aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                map[alias.Trim()] = definition.Key;
                map[NormalizeKeyCandidate(alias)] = definition.Key;
            }
        }

        return map;
    }

    private static bool IsChineseLocale(string? locale)
        => !string.IsNullOrWhiteSpace(locale)
           && locale.StartsWith("zh", StringComparison.OrdinalIgnoreCase);

    private static bool IsKeyCandidate(string value)
        => KeyCandidateRegex().IsMatch(value);

    private static string NormalizeKeyCandidate(string value)
    {
        var trimmed = value.Trim().Replace('-', '_').Replace(' ', '_');
        return MultiUnderscoreRegex().Replace(trimmed.ToLowerInvariant(), "_");
    }

    private static string HumanizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var parts = value
            .Split(['_'], StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..])
            .ToList();

        return parts.Count == 0 ? value : string.Join(' ', parts);
    }

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex KeyCandidateRegex();

    [GeneratedRegex("_+")]
    private static partial Regex MultiUnderscoreRegex();
}
