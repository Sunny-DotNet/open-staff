using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Agent.Vendor.Anthropic;

public class AnthropicAgentProvider : IVendorAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IModelDataSource? _modelDataSource;

    /// <summary>models.dev 中的供应商 ID</summary>
    private const string VendorId = "anthropic";

    /// <summary>当 IModelDataSource 不可用时使用的静态模型列表</summary>
    private static readonly VendorModel[] FallbackModels =
    [
        new("claude-sonnet-4-20250514", "Claude Sonnet 4", "Claude 4"),
        new("claude-opus-4-20250514", "Claude Opus 4", "Claude 4"),
        new("claude-3-5-haiku-20241022", "Claude Haiku 3.5", "Claude 3.5")
    ];

    public AnthropicAgentProvider(ILoggerFactory loggerFactory, IModelDataSource? modelDataSource = null)
    {
        _loggerFactory = loggerFactory;
        _modelDataSource = modelDataSource;
    }

    public string ProviderType => "anthropic";
    public string DisplayName => "Anthropic Claude";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/anthropic.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "Anthropic Claude 系列模型（Claude Sonnet、Opus、Haiku 等）",
        Fields =
        [
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "claude-sonnet-4-20250514",
            }
        ]
    };

    public async Task<IReadOnlyList<VendorModel>> GetModelsAsync(CancellationToken ct = default)
    {
        if (_modelDataSource is { IsReady: true })
        {
            var models = await _modelDataSource.GetModelsByVendorAsync(VendorId, ct);
            if (models.Count > 0)
                return models.Select(m => new VendorModel(m.Id, m.Name, m.Family)).ToList();
        }
        return FallbackModels;
    }

    public Task<AIAgent> CreateAgentAsync(AgentRole role, AgentContext context, ResolvedProvider provider)
    {
        var config = AgentConfig.FromJson(role.Config);
        var apiKey = provider.ApiKey
            ?? throw new InvalidOperationException("请先在供应商管理中配置 Anthropic API Key");
        var model = config.Get("model") ?? "claude-sonnet-4-20250514";
        var systemPrompt = role.SystemPrompt ?? "";

        var client = new AnthropicClient { ApiKey = apiKey };
        AIAgent agent = client.AsAIAgent(
            model: model,
            name: role.Name,
            instructions: systemPrompt);
        return Task.FromResult(agent);
    }
}
