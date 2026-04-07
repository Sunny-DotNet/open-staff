using System.Reflection;
using OpenStaff.Core.Agents;

namespace OpenStaff.Agent.Builtin.Prompts;

/// <summary>
/// 从嵌入资源加载提示词
/// </summary>
public class EmbeddedPromptLoader : IPromptLoader
{
    private readonly Assembly _assembly;
    private readonly Dictionary<string, string> _cache = new();

    public EmbeddedPromptLoader()
    {
        _assembly = typeof(EmbeddedPromptLoader).Assembly;
    }

    public string Load(string promptName, string language)
    {
        var lang = NormalizeLanguage(language);
        var cacheKey = $"{promptName}.{lang}";

        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        var content = LoadResource($"{promptName}.{lang}.txt")
            ?? LoadResource($"{promptName}.zh-Hans.txt")
            ?? $"[Prompt not found: {promptName}.{lang}]";

        _cache[cacheKey] = content;
        return content;
    }

    private string? LoadResource(string fileName)
    {
        var resourceName = _assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($".Prompts.{fileName}", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return null;

        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static string NormalizeLanguage(string language)
    {
        if (string.IsNullOrEmpty(language)) return "zh-Hans";

        return language.ToLowerInvariant() switch
        {
            "zh-cn" or "zh-hans" or "zh" => "zh-Hans",
            "en-us" or "en" => "en",
            _ => language.Contains("zh") ? "zh-Hans" : "en"
        };
    }
}
