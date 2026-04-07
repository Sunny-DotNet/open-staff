using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Plugins.ModelDataSource;
using System.ClientModel;

namespace OpenStaff.Agent.Vendor.OpenAI;

public class OpenAIAgentProvider : IVendorAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IModelDataSource? _modelDataSource;

    private const string VendorId = "openai";

    private static readonly VendorModel[] FallbackModels =
    [
        new("gpt-4o", "GPT-4o", "GPT-4o"),
        new("gpt-4o-mini", "GPT-4o Mini", "GPT-4o"),
        new("gpt-4.1", "GPT-4.1", "GPT-4.1"),
        new("gpt-4.1-mini", "GPT-4.1 Mini", "GPT-4.1"),
        new("o3-mini", "o3-mini", "o3")
    ];

    public OpenAIAgentProvider(ILoggerFactory loggerFactory, IModelDataSource? modelDataSource = null)
    {
        _loggerFactory = loggerFactory;
        _modelDataSource = modelDataSource;
    }

    public string ProviderType => "openai";
    public string DisplayName => "OpenAI";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/openai.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "OpenAI GPT 系列模型",
        Fields =
        [
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "gpt-4o",
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
        var apiKey = provider.ApiKey
            ?? throw new InvalidOperationException("请先在供应商管理中配置 OpenAI API Key");

        var config = AgentConfig.FromJson(role.Config);
        var model = config.Get("model") ?? "gpt-4o";
        var systemPrompt = role.SystemPrompt ?? "";

        var credential = new ApiKeyCredential(apiKey);
        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(provider.BaseUrl))
            options.Endpoint = new Uri(provider.BaseUrl);

        var client = new OpenAIClient(credential, options);
        IChatClient chatClient = client.GetChatClient(model).AsIChatClient();

        AIAgent agent = new ChatClientAgent(
            chatClient,
            name: role.Name,
            instructions: systemPrompt,
            loggerFactory: _loggerFactory);
        return Task.FromResult(agent);
    }
}
