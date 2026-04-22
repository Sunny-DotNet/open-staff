using OpenStaff.AgentSouls.Dtos;
using OpenStaff.AgentSouls.Services;
using OpenStaff.ApiServices;

namespace OpenStaff.Tests.Unit;

public class AgentSoulApiServiceTests
{
    [Fact]
    public async Task GetOptionsAsync_UsesRequestedLocaleWithFallback()
    {
        var service = new AgentSoulApiService(new FakeAgentSoulService(
            new FakeAgentSoulHttpService(
            [
                new AgentSoulValue("adaptable", new Dictionary<string, string>
                {
                    ["en"] = "Adaptable",
                    ["zh"] = "适应力强的"
                })
            ]),
            new FakeAgentSoulHttpService(
            [
                new AgentSoulValue("collaborative", new Dictionary<string, string>
                {
                    ["en"] = "Collaborative",
                    ["zh"] = "注重协作的"
                })
            ]),
            new FakeAgentSoulHttpService(
            [
                new AgentSoulValue("formal", new Dictionary<string, string>
                {
                    ["en"] = "Formal",
                    ["zh"] = "正式严谨的"
                })
            ])));

        var result = await service.GetOptionsAsync("zh-CN");

        Assert.Equal("适应力强的", Assert.Single(result.Traits).Label);
        Assert.Equal("注重协作的", Assert.Single(result.Attitudes).Label);
        Assert.Equal("正式严谨的", Assert.Single(result.Styles).Label);
    }

    private sealed class FakeAgentSoulService : IAgentSoulService
    {
        public FakeAgentSoulService(
            IAgentSoulHttpService personalityTraits,
            IAgentSoulHttpService workAttitudes,
            IAgentSoulHttpService communicationStyles)
        {
            PersonalityTraits = personalityTraits;
            WorkAttitudes = workAttitudes;
            CommunicationStyles = communicationStyles;
        }

        public IAgentSoulHttpService CommunicationStyles { get; }

        public IAgentSoulHttpService PersonalityTraits { get; }

        public IAgentSoulHttpService WorkAttitudes { get; }
    }

    private sealed class FakeAgentSoulHttpService : IAgentSoulHttpService
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _dictionary;
        private readonly IReadOnlyCollection<AgentSoulValue> _values;

        public FakeAgentSoulHttpService(IReadOnlyCollection<AgentSoulValue> values)
        {
            _values = values;
            _dictionary = values.ToDictionary(
                value => value.Key,
                value => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(value.Aliases, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }

        public string DefaultAliasName => "en";

        public Task<IReadOnlyCollection<AgentSoulValue>> GetAllAsync() => Task.FromResult(_values);

        public Task<IReadOnlyDictionary<string, string>> GetAllByLocaleAsync(string? locale = null)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

        public Task<string> GetAsync(string key, string? locale = null)
        {
            if (!_dictionary.TryGetValue(key, out var aliases))
            {
                throw new KeyNotFoundException(key);
            }

            foreach (var candidate in EnumerateLocaleCandidates(locale))
            {
                if (aliases.TryGetValue(candidate, out var localized))
                {
                    return Task.FromResult(localized);
                }
            }

            if (aliases.TryGetValue(DefaultAliasName, out var fallback))
            {
                return Task.FromResult(fallback);
            }

            throw new KeyNotFoundException(key);
        }

        private static IEnumerable<string> EnumerateLocaleCandidates(string? locale)
        {
            if (string.IsNullOrWhiteSpace(locale))
            {
                yield break;
            }

            var normalized = locale.Trim();
            yield return normalized;

            var separatorIndex = normalized.IndexOfAny(['-', '_']);
            if (separatorIndex > 0)
            {
                yield return normalized[..separatorIndex];
            }
        }
    }
}
