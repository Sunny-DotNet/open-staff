using Google.GenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Plugins.ModelDataSource;

namespace OpenStaff.Agent.Vendor.Google;

public class GoogleAgentProvider : IVendorAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IModelDataSource? _modelDataSource;

    private const string VendorId = "google";

    private static readonly VendorModel[] FallbackModels =
    [
        new("gemini-2.5-flash", "Gemini 2.5 Flash", "Gemini 2.5"),
        new("gemini-2.5-pro", "Gemini 2.5 Pro", "Gemini 2.5"),
        new("gemini-2.0-flash", "Gemini 2.0 Flash", "Gemini 2.0")
    ];

    public GoogleAgentProvider(ILoggerFactory loggerFactory, IModelDataSource? modelDataSource = null)
    {
        _loggerFactory = loggerFactory;
        _modelDataSource = modelDataSource;
    }

    public string ProviderType => "google";
    public string DisplayName => "Google Gemini";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/gemini.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "Google Gemini 系列模型",
        Fields =
        [
            new AgentConfigField
            {
                Key = "model",
                Label = "模型",
                FieldType = "select",
                Required = true,
                DefaultValue = "gemini-2.5-flash",
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
            ?? throw new InvalidOperationException("请先在供应商管理中配置 Google API Key");
        var model = config.Get("model") ?? "gemini-2.5-flash";
        var systemPrompt = role.SystemPrompt ?? "";

        var client = new Client(vertexAI: false, apiKey: apiKey);
        IChatClient chatClient = client.AsIChatClient(model);

        AIAgent agent = new ChatClientAgent(
            chatClient,
            name: role.Name,
            instructions: systemPrompt,
            loggerFactory: _loggerFactory);
        return Task.FromResult(agent);
    }
}
