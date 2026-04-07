using GitHub.Copilot.SDK;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Plugins.ModelDataSource;
using System.ClientModel;
using System.ClientModel.Primitives;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

public class GitHubCopilotAgentProvider : IVendorAgentProvider
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<GitHubCopilotAgentProvider> _logger;
    private readonly IModelDataSource? _modelDataSource;

    private const string VendorId = "github";

    private static readonly VendorModel[] FallbackModels =
    [
        new("gpt-4o", "GPT-4o", "GPT-4o"),
        new("gpt-4o-mini", "GPT-4o Mini", "GPT-4o"),
        new("claude-sonnet-4-20250514", "Claude Sonnet 4", "Claude 4"),
        new("gemini-2.5-flash", "Gemini 2.5 Flash", "Gemini 2.5")
    ];

    public GitHubCopilotAgentProvider(ILoggerFactory loggerFactory, IModelDataSource? modelDataSource = null)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<GitHubCopilotAgentProvider>();
        _modelDataSource = modelDataSource;
    }

    public string ProviderType => "github-copilot";
    public string DisplayName => "GitHub Copilot";

    public string? AvatarDataUri => "https://unpkg.com/@lobehub/icons-static-png@latest/light/githubcopilot.png";

    public AgentConfigSchema GetConfigSchema() => new()
    {
        ProviderType = ProviderType,
        DisplayName = DisplayName,
        Description = "GitHub Copilot 智能体（通过设备授权自动获取 Token）",
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

    public async Task<AIAgent> CreateAgentAsync(AgentRole role, AgentContext context, ResolvedProvider provider)
    {
        // ── 1. 启动 Copilot CLI 客户端 ──
        var copilotClient = new CopilotClient();
        await copilotClient.StartAsync();
        _logger.LogInformation("Copilot CLI 客户端已启动");

        // ── 2. 会话配置 ──
        var sessionConfig = new SessionConfig
        {
            Streaming = true,
            // 权限回调（必填）：当前阶段自动批准，后续将接入"秘书"审批流程
            OnPermissionRequest = (request, _) =>
            {
                _logger.LogInformation("权限请求: {Kind}", request.Kind);
                return Task.FromResult(new PermissionRequestResult
                {
                    Kind = PermissionRequestResultKind.Approved
                });
            },
            // 用户输入回调（可选）
            OnUserInputRequest = (request, _) =>
            {
                _logger.LogInformation("Agent 提问: {Question}", request.Question);
                return Task.FromResult(new UserInputResponse
                {
                    Answer = "继续",
                    WasFreeform = true
                });
            }
        };

        // ── 3. 通过 Agent Framework 包装为 AIAgent ──
        // ownsClient: true → Agent Dispose 时自动关闭 CopilotClient
        return copilotClient.AsAIAgent(sessionConfig, ownsClient: true);
    }
}
