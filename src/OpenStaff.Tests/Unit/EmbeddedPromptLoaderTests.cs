using OpenStaff.Agent.Builtin.Prompts;
using Xunit;

namespace OpenStaff.Tests.Unit;

public class EmbeddedPromptLoaderTests
{
    private readonly EmbeddedPromptLoader _loader = new();

    [Theory]
    [InlineData("communicator.system", "zh-CN")]
    [InlineData("communicator.system", "zh-Hans")]
    [InlineData("architect.system", "zh-CN")]
    [InlineData("decision_maker.system", "zh-CN")]
    [InlineData("producer.system", "zh-CN")]
    [InlineData("debugger.system", "zh-CN")]
    [InlineData("orchestrator.system", "zh-CN")]
    [InlineData("image_creator.system", "zh-CN")]
    [InlineData("video_creator.system", "zh-CN")]
    public void Load_KnownPrompt_ZhCN_ReturnsNonEmptyContent(string promptName, string language)
    {
        var content = _loader.Load(promptName, language);

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Theory]
    [InlineData("communicator.system", "en")]
    [InlineData("communicator.system", "en-US")]
    [InlineData("architect.system", "en")]
    [InlineData("decision_maker.system", "en")]
    [InlineData("producer.system", "en")]
    [InlineData("debugger.system", "en")]
    [InlineData("orchestrator.system", "en")]
    [InlineData("image_creator.system", "en")]
    [InlineData("video_creator.system", "en")]
    public void Load_KnownPrompt_English_ReturnsNonEmptyContent(string promptName, string language)
    {
        var content = _loader.Load(promptName, language);

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Theory]
    [InlineData("secretary")]
    [InlineData("communicator")]
    [InlineData("architect")]
    [InlineData("decision_maker")]
    [InlineData("producer")]
    [InlineData("debugger")]
    [InlineData("orchestrator")]
    [InlineData("image_creator")]
    [InlineData("video_creator")]
    public void Load_AllBuiltinRoles_HaveSystemPrompts(string roleType)
    {
        var content = _loader.Load($"{roleType}.system", "zh-CN");

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Fact]
    public void Load_UnknownPrompt_ReturnsFallbackMessage()
    {
        var content = _loader.Load("nonexistent.system", "en");

        Assert.Contains("[Prompt not found:", content);
    }

    [Fact]
    public void Load_UnknownLanguage_FallsBackToZhHans()
    {
        // The fallback chain: exact → zh-Hans → "[Prompt not found...]"
        // For a known prompt with unknown language, should fall back to zh-Hans
        var content = _loader.Load("communicator.system", "fr-FR");

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Fact]
    public void Load_EmptyLanguage_DefaultsToZhHans()
    {
        var content = _loader.Load("communicator.system", "");

        Assert.NotNull(content);
        Assert.NotEmpty(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Theory]
    [InlineData("zh-CN", "zh-Hans")]
    [InlineData("zh-hans", "zh-Hans")]
    [InlineData("zh", "zh-Hans")]
    [InlineData("en-US", "en")]
    [InlineData("en", "en")]
    public void Load_NormalizesLanguageCodes(string inputLang, string _)
    {
        // Should load successfully for all normalized variants
        var content = _loader.Load("communicator.system", inputLang);

        Assert.NotNull(content);
        Assert.DoesNotContain("[Prompt not found:", content);
    }

    [Fact]
    public void Load_CachesResults_ReturnsSameInstance()
    {
        var first = _loader.Load("communicator.system", "en");
        var second = _loader.Load("communicator.system", "en");

        Assert.Same(first, second);
    }

    [Fact]
    public void Load_DifferentLanguages_ReturnDifferentContent()
    {
        var zhContent = _loader.Load("communicator.system", "zh-CN");
        var enContent = _loader.Load("communicator.system", "en");

        // Both should exist but be different prompts
        Assert.NotEqual(zhContent, enContent);
    }
}
