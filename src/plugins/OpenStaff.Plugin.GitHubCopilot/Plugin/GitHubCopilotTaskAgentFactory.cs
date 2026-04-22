using System.Diagnostics;
using System.Text.Json;
using GitHub.Copilot.SDK;
using Microsoft.Agents.AI.GitHub.Copilot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenHub.Agents;
using OpenStaff.Agents;
using OpenStaff.Agent;
using OpenStaff.Agent.Services;
using OpenStaff.Core.Agents;
using OpenStaff.Options;
using OpenStaff.Provider.Platforms;

namespace OpenStaff.Agent.Vendor.GitHubCopilot;

internal sealed class GitHubCopilotTaskAgentFactory : ITaskAgentFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly GitHubCopilotConfigurationService _configurationService;
    private readonly IGitHubCopilotClientHost _clientHost;
    private readonly IPermissionRequestHandler _permissionRequestHandler;
    private readonly OpenStaffOptions _openStaffOptions;

    public GitHubCopilotTaskAgentFactory(
        IServiceProvider serviceProvider,
        GitHubCopilotConfigurationService configurationService,
        IGitHubCopilotClientHost clientHost,
        IPermissionRequestHandler permissionRequestHandler,
        IOptions<OpenStaffOptions> openStaffOptions)
    {
        _serviceProvider = serviceProvider;
        _configurationService = configurationService;
        _clientHost = clientHost;
        _permissionRequestHandler = permissionRequestHandler;
        _openStaffOptions = openStaffOptions.Value;
    }

    public async Task<ITaskAgent> CreateAsync(TaskAgentCreateRequest request, CancellationToken cancellationToken = default)
    {
        var rawContext = request.RawContext as AgentContext
            ?? throw new InvalidOperationException("GitHub Copilot task agent requires a concrete AgentContext.");
        var providerConfiguration = await _configurationService.GetEffectiveConfigurationAsync(cancellationToken);
        var client = await _clientHost.GetClientAsync(cancellationToken);
        var conversationIdentity = GitHubCopilotConversationIdentity.Create(rawContext);
        // Copilot 这里也不是把 Skill 变成 tool，而是把技能目录交给会话做发现。
        var resolvedSkillDirectories = rawContext.GetResolvedSkillDirectories();
        var skillDiscoveryDirectories = resolvedSkillDirectories
            .Select(Path.GetDirectoryName)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sessionConfig = new SessionConfig
        {
            SessionId = conversationIdentity.CopilotSessionId,
            ClientName = "OpenStaff",
            Model = request.Role.ModelName,
            WorkingDirectory = ResolveWorkingDirectory(rawContext),
            Streaming = providerConfiguration.Streaming!.Value,
            // Copilot SDK 需要的是“技能发现目录”，所以这里传父目录而不是单个技能目录本身。
            SkillDirectories = skillDiscoveryDirectories.Count > 0 ? [.. skillDiscoveryDirectories] : null,
            SystemMessage = BuildSystemMessage(request.Role.SystemPrompt),
            OnPermissionRequest = (permissionRequest, invocation) => HandlePermissionRequestAsync(permissionRequest, invocation, rawContext, providerConfiguration.AutoApproved!.Value),
            OnUserInputRequest = (userInputRequest, _) => HandleUserInputRequestAsync(userInputRequest, providerConfiguration.AutoApproved!.Value)
        };

        var innerAgent = new GitHubCopilotAgent(
            client,
            sessionConfig,
            ownsClient: false,
            id: request.Role.Id.ToString("N"),
            name: request.Role.Name,
            description: request.Role.Description ?? request.Role.JobTitle);

        var executionAgent = new GitHubCopilotTraceAgent(innerAgent, client, _clientHost, sessionConfig, resolvedSkillDirectories);
        ITaskAgent taskAgent = new LazySharedCopilotTaskAgent(async ct =>
        {
            if (client.State != ConnectionState.Connected)
                await client.StartAsync(ct);

            var sharedSession = await client.CreateSessionAsync(sessionConfig, ct);
            return sharedSession.AsTaskAgent(ownsSession: true);
        });

        return taskAgent.AsFeatureTaskAgent(_serviceProvider, executionAgent, innerAgent);
    }

    private async Task<PermissionRequestResult> HandlePermissionRequestAsync(
        PermissionRequest request,
        PermissionInvocation invocation,
        OpenStaff.Core.Agents.AgentContext context,
        bool autoApproved)
    {
        if (autoApproved)
        {
            return new PermissionRequestResult
            {
                Kind = PermissionRequestResultKind.Approved
            };
        }

        var authorizationResult = await _permissionRequestHandler.HandleAsync(
            CreatePermissionAuthorizationRequest(request, invocation, context));

        return new PermissionRequestResult
        {
            Kind = MapPermissionResultKind(authorizationResult)
        };
    }

    private static Task<UserInputResponse> HandleUserInputRequestAsync(UserInputRequest request, bool autoApproved)
    {
        if (!autoApproved)
            throw new NotImplementedException("GitHub Copilot 结构化交互通道尚未实现；AutoApproved=false 时暂不支持用户输入请求。");

        return Task.FromResult(new UserInputResponse
        {
            Answer = "继续",
            WasFreeform = true
        });
    }

    private static SystemMessageConfig? BuildSystemMessage(string? systemPrompt)
    {
        if (string.IsNullOrWhiteSpace(systemPrompt))
            return null;

        return new SystemMessageConfig
        {
            Mode = SystemMessageMode.Append,
            Content = systemPrompt
        };
    }

    private static PermissionAuthorizationRequest CreatePermissionAuthorizationRequest(
        PermissionRequest request,
        PermissionInvocation invocation,
        OpenStaff.Core.Agents.AgentContext context)
    {
        return new PermissionAuthorizationRequest
        {
            Kind = request.Kind,
            Message = BuildPermissionMessage(request),
            SessionId = context.SessionId,
            ProjectId = context.ProjectId,
            AgentInstanceId = context.AgentInstanceId,
            ProjectAgentRoleId = context.ProjectAgentRoleId,
            CopilotSessionId = invocation.SessionId,
            RoleName = context.Role?.Name,
            ProjectName = context.Project?.Name,
            Scene = context.Scene?.ToString(),
            DispatchSource = context.ExtraConfig.TryGetValue("openstaff_dispatch_source", out var dispatchSource)
                ? dispatchSource?.ToString()
                : null,
            ToolCallId = request switch
            {
                PermissionRequestShell shell => shell.ToolCallId,
                PermissionRequestWrite write => write.ToolCallId,
                PermissionRequestRead read => read.ToolCallId,
                PermissionRequestMcp mcp => mcp.ToolCallId,
                PermissionRequestUrl url => url.ToolCallId,
                PermissionRequestMemory memory => memory.ToolCallId,
                PermissionRequestCustomTool customTool => customTool.ToolCallId,
                PermissionRequestHook hook => hook.ToolCallId,
                _ => null
            },
            ToolName = request switch
            {
                PermissionRequestMcp mcp => mcp.ToolName,
                PermissionRequestCustomTool customTool => customTool.ToolName,
                _ => null
            },
            FileName = request switch
            {
                PermissionRequestWrite write => write.FileName,
                _ => null
            },
            Url = request switch
            {
                PermissionRequestUrl url => url.Url,
                _ => null
            },
            CommandText = request switch
            {
                PermissionRequestShell shell => shell.FullCommandText,
                _ => null
            },
            Warning = request switch
            {
                PermissionRequestShell shell => shell.Warning,
                _ => null
            },
            DetailsJson = JsonSerializer.Serialize(request, request.GetType())
        };
    }

    private static string BuildPermissionMessage(PermissionRequest request)
    {
        return request switch
        {
            PermissionRequestShell shell => $"允许执行命令：{shell.FullCommandText}",
            PermissionRequestWrite write => $"允许写入文件：{write.FileName}",
            PermissionRequestRead read => $"允许读取路径：{read.Path}",
            PermissionRequestMcp mcp => $"允许调用 MCP 工具：{mcp.ToolTitle} ({mcp.ServerName})",
            PermissionRequestUrl url => $"允许访问 URL：{url.Url}",
            PermissionRequestMemory memory => $"允许写入记忆：{memory.Subject}",
            PermissionRequestCustomTool customTool => $"允许调用自定义工具：{customTool.ToolName}",
            PermissionRequestHook hook => $"允许执行 Hook：{hook.ToolName}",
            _ => $"允许执行 {request.Kind} 操作"
        };
    }

    private static PermissionRequestResultKind MapPermissionResultKind(PermissionAuthorizationResult result)
    {
        if (result.Kind == PermissionAuthorizationKind.Accept)
            return PermissionRequestResultKind.Approved;

        return result.Source switch
        {
            PermissionAuthorizationSource.InteractiveClient
                or PermissionAuthorizationSource.InteractiveListener
                => PermissionRequestResultKind.DeniedInteractivelyByUser,
            _ => PermissionRequestResultKind.DeniedCouldNotRequestFromUser
        };
    }

    private string ResolveWorkingDirectory(AgentContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Project?.WorkspacePath))
            return context.Project.WorkspacePath;

        return _openStaffOptions.WorkingDirectory;
    }
}
