using OpenStaff.Application.AgentRoles;
using OpenStaff.Application.AgentRoles.Services;
using OpenStaff.Application.Conversations.Models;
using OpenStaff.Application.Conversations.Services;
using OpenStaff.AgentSouls.Services;
using OpenStaff.Core.Agents;

namespace OpenStaff.ApiServices;
/// <summary>
/// 智能体角色应用服务实现。
/// Application service implementation for agent role management.
/// </summary>
public class AgentRoleApiService
    : CrudApiServiceBase<AgentRole, AgentRoleDto, Guid, AgentRoleQueryInput, CreateAgentRoleInput, UpdateAgentRoleInput>,
      IAgentRoleApiService
{
    private static readonly AgentRoleMapper Mapper = new();
    private readonly IAgentRoleRepository _agentRoles;
    private readonly IRepositoryContext _repositoryContext;
    private readonly ProviderAccountService _accountService;
    private readonly IVendorPlatformCatalog _vendorPlatforms;
    private readonly IAgentService _agentService;
    private readonly SessionStreamManager _streamManager;
    private readonly AgentRoleTemplateImportService? _roleTemplateImportService;
    private readonly ConversationEntryService? _conversationEntryService;
    private readonly IAgentSoulService? _agentSoulService;

    /// <summary>
    /// 初始化智能体角色应用服务。
    /// Initializes the agent role application service.
    /// </summary>
    public AgentRoleApiService(
        IAgentRoleRepository agentRoles,
        IRepositoryContext repositoryContext,
        ProviderAccountService accountService,
        IVendorPlatformCatalog vendorPlatforms,
        IAgentService agentService,
        SessionStreamManager streamManager,
        AgentRoleTemplateImportService? roleTemplateImportService = null,
        ConversationEntryService? conversationEntryService = null,
        IAgentSoulService? agentSoulService = null,
        IServiceProvider? serviceProvider = null)
        : base(serviceProvider, agentRoles, repositoryContext)
    {
        _agentRoles = agentRoles;
        _repositoryContext = repositoryContext;
        _accountService = accountService;
        _vendorPlatforms = vendorPlatforms;
        _agentService = agentService;
        _streamManager = streamManager;
        _roleTemplateImportService = roleTemplateImportService;
        _conversationEntryService = conversationEntryService;
        _agentSoulService = agentSoulService;
    }

    /// <inheritdoc />
    public async Task<List<AgentRoleDto>> GetAllAsync(CancellationToken ct = default)
        => (await GetAllAsync(new AgentRoleQueryInput(), ct)).Items;

    /// <inheritdoc />
    public override async Task<PagedResult<AgentRoleDto>> GetAllAsync(AgentRoleQueryInput input, CancellationToken ct = default)
    {
        input ??= new AgentRoleQueryInput();
        var roles = await _agentRoles
            .Where(r => r.IsActive)
            .OrderBy(r => r.IsBuiltin ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        var providerNames = await ResolveProviderNamesAsync(roles);

        var result = roles.Select(role => BuildDto(
            role,
            role.ModelProviderId.HasValue && providerNames.TryGetValue(role.ModelProviderId.Value, out var name) ? name : null,
            GetVendorAvatar(role))).ToList();

        // 追加未物化的 Vendor 虚拟条目
        var materializedProviderTypes = roles
            .Where(r => r.Source == AgentSource.Vendor)
            .Select(r => r.ProviderType)
            .Where(pt => !string.IsNullOrEmpty(pt))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var vendorPlatform in _vendorPlatforms.Platforms.Values)
        {
            var metadata = vendorPlatform.Metadata;
            if (materializedProviderTypes.Contains(metadata.ProviderType)) continue;

            result.Add(new AgentRoleDto
            {
                Id = Guid.Empty,
                Name = metadata.DisplayName,
                RoleType = metadata.ProviderType,
                Description = null,
                Avatar = metadata.AvatarDataUri,
                IsBuiltin = false,
                IsVirtual = true,
                Source = AgentSource.Vendor,
                ProviderType = metadata.ProviderType,
                CreatedAt = DateTime.MinValue
            });
        }

        var total = result.Count;
        var page = input.Page < 1 ? 1 : input.Page;
        var pageSize = input.PageSize < 1 ? total == 0 ? 1 : total : input.PageSize;
        var skip = (page - 1L) * pageSize;
        var items = result.Skip((int)Math.Min(skip, int.MaxValue)).Take(pageSize).ToList();

        return new PagedResult<AgentRoleDto>(items, total);
    }

    /// <inheritdoc />
    public override async Task<AgentRoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _agentRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) return null;

        return await BuildDtoAsync(role);
    }

    /// <inheritdoc />
    public override async Task<AgentRoleDto> CreateAsync(CreateAgentRoleInput input, CancellationToken ct = default)
    {
        input.JobTitle = AgentJobTitleCatalog.NormalizeKey(input.JobTitle);
        input.Soul = await NormalizeSoulInputAsync(input.Soul);
        var role = MapToEntity(input);
        await Repository.AddAsync(role, ct);
        await _repositoryContext.SaveChangesAsync(ct);
        return await BuildDtoAsync(role);
    }

    /// <inheritdoc />
    public override async Task<AgentRoleDto> UpdateAsync(Guid id, UpdateAgentRoleInput input, CancellationToken ct = default)
    {
        input.JobTitle = AgentJobTitleCatalog.NormalizeKey(input.JobTitle);
        input.Soul = await NormalizeSoulInputAsync(input.Soul);
        var role = await _agentRoles.FindAsync(id, ct);
        if (role == null)
            throw CreateEntityNotFoundException(id);

        MapToEntity(input, role);
        role.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);

        return await BuildDtoAsync(role);
    }

    /// <inheritdoc />
    public Task<PreviewAgentRoleTemplateImportResultDto> PreviewTemplateImportAsync(
        PreviewAgentRoleTemplateImportInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        return GetRoleTemplateImportService().PreviewAsync(input.Content, ct);
    }

    /// <inheritdoc />
    public async Task<ImportAgentRoleTemplateResultDto> ImportTemplateAsync(
        ImportAgentRoleTemplateInput input,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = await GetRoleTemplateImportService().ImportAsync(input.Content, input.OverwriteExisting, ct);
        return new ImportAgentRoleTemplateResultDto
        {
            Role = await BuildDtoAsync(result.Role),
            Preview = result.Preview,
            AddedMcpBindings = result.AddedMcpBindings,
            AddedSkillBindings = result.AddedSkillBindings,
        };
    }

    /// <inheritdoc />
    public override async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _agentRoles.FindAsync(id, ct);
        if (role == null)
            throw CreateEntityNotFoundException(id);

        if (role.IsBuiltin) throw new InvalidOperationException("不能删除内置角色");
        if (role.Source == AgentSource.Vendor) throw new InvalidOperationException("Vendor 角色不能删除，请使用重置");

        role.IsActive = false;
        role.UpdatedAt = DateTime.UtcNow;
        await _repositoryContext.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task<bool> ResetVendorAsync(string providerType, CancellationToken ct)
    {
        var role = await _agentRoles
            .FirstOrDefaultAsync(r => r.Source == AgentSource.Vendor && r.ProviderType == providerType && r.IsActive, ct);
        if (role == null) return false;

        // 硬删除，恢复为虚拟状态
        _agentRoles.Remove(role);
        await _repositoryContext.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    public async Task<ConversationTaskOutput> TestChatAsync(TestChatRequest request, CancellationToken ct)
    {
        // zh-CN: 新链路优先走统一入口服务，原因是测试对话虽然仍是瞬态会话，但它也属于“对话入口”的一种，
        // 不应该继续由 API Service 自己手写运行时请求。
        // 这里保留旧实现作为兼容后备，是为了让现有隔离单测和部分旧宿主在未注入新服务时仍可运行。
        // en: Prefer the unified entry service while retaining the legacy path as a compatibility fallback for isolated tests and older hosts.
        if (_conversationEntryService != null)
        {
            return await _conversationEntryService.StartTestChatAsync(
                new TestChatEntry(request.AgentRoleId, request.Message, Override: request.Override),
                ct);
        }

        var id = request.AgentRoleId;
        var message = request.Message;
        var liveOverride = request.Override;

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required");

        var sourceRole = await _agentRoles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (sourceRole == null) throw new KeyNotFoundException("Agent role not found");

        // 这里只是为了当前测试会话构造一个“展示用/理解用”的有效角色快照。
        // 真正的运行时不会直接拿这个内存对象去执行，而是会在下游再次根据 AgentRoleId
        // 从数据库重新解析角色，再叠加 OverrideJson 生成最终执行画像。
        var role = CreateEffectiveTestRole(sourceRole, liveOverride);

        var sessionId = Guid.NewGuid();
        var stream = _streamManager.Create(sessionId);
        // 测试对话没有落库正式会话，这里只创建一个瞬态流，把用户输入先推给前端，
        // 这样界面可以立刻显示“我刚刚发了什么”。
        stream.Push(SessionEventTypes.UserInput, payload: JsonSerializer.Serialize(new { content = message }));

        var runtimeRequest = new CreateMessageRequest(
            Scene: MessageScene.Test,
            MessageContext: new MessageContext(
                ProjectId: null,
                SessionId: sessionId,
                ParentMessageId: null,
                FrameId: null,
                ParentFrameId: null,
                TaskId: null,
                ProjectAgentRoleId: null,
                InitiatorRole: null,
                Extra: null),
            InputRole: ChatRole.User,
            Input: message,
            AgentRoleId: id,
            // 这里传下去的是“角色 Id + 覆盖 JSON”，而不是上面那个 role 实例本身。
            // 所以想看 MCP / Skill 为什么没有生效，重点要查运行时如何按 Id 重新装配角色。
            OverrideJson: liveOverride == null ? null : JsonSerializer.Serialize(liveOverride));

        var response = await _agentService.CreateMessageAsync(runtimeRequest, ct);
        if (!_agentService.TryGetMessageHandler(response.MessageId, out var handler) || handler == null)
            throw new InvalidOperationException($"Message handler '{response.MessageId}' was not created.");

        // 后台等待运行时结束，再把最终结果整理成测试对话需要的事件格式。
        _ = Task.Run(() => CompleteTestChatStreamAsync(sessionId, stream, handler, role), CancellationToken.None);

        return new ConversationTaskOutput
        {
            TaskId = sessionId,
            Status = ExecutionPackageStatus.Active,
            SessionId = sessionId,
            Scene = SessionSceneTypes.Test,
            EntryKind = ExecutionEntryKinds.TestChat,
            AgentRoleId = id,
            IsAwaitingInput = false
        };
    }

    /// <summary>
    /// 基于源角色与临时覆盖配置构造测试执行时使用的有效角色快照。
    /// Builds the effective role snapshot used for test execution from the source role and optional live overrides.
    /// </summary>
    /// <param name="sourceRole">原始角色定义。/ Original role definition.</param>
    /// <param name="liveOverride">仅在本次测试生效的临时覆盖。/ Temporary overrides that apply only to the current test run.</param>
    /// <returns>用于本次测试调用的合成角色。/ The composed role used for the current test invocation.</returns>
    /// <remarks>
    /// zh-CN: 该方法只生成内存中的执行配置，不会回写数据库或修改原始角色生命周期。
    /// en: This method creates an in-memory execution profile only; it does not persist changes or alter the source role lifecycle.
    /// </remarks>
    private static AgentRole CreateEffectiveTestRole(AgentRole sourceRole, AgentRoleInput? liveOverride)
    {
        return AgentRoleExecutionProfileFactory.CreateEffectiveRole(sourceRole, liveOverride);
    }

    /// <summary>
    /// 完成测试聊天流并将最终结果推送到临时会话流。
    /// Completes the test chat stream and pushes the final result into the transient session stream.
    /// </summary>
    /// <param name="sessionId">临时测试会话标识。/ Transient test session identifier.</param>
    /// <param name="stream">接收事件的会话流。/ Session stream that receives emitted events.</param>
    /// <param name="handler">跟踪消息执行完成情况的处理器。/ Handler that tracks message execution completion.</param>
    /// <param name="role">本次测试使用的角色快照。/ Role snapshot used for the test run.</param>
    /// <returns>表示异步收尾过程的任务。/ A task representing the asynchronous completion flow.</returns>
    /// <remarks>
    /// zh-CN: 该方法负责测试流的生命周期收尾：等待完成、推送 done/error 事件、移除消息处理器，并结束瞬态流。
    /// en: This method owns the test-stream shutdown lifecycle: it awaits completion, emits done/error events, removes the message handler, and completes the transient stream.
    /// </remarks>
    private async Task CompleteTestChatStreamAsync(
        Guid sessionId,
        SessionStream stream,
        MessageHandler handler,
        AgentRole role)
    {
        try
        {
            var summary = await handler.Completion;
            var runtimeRole = role.JobTitle
                ?? (!string.IsNullOrWhiteSpace(summary.AgentRole) ? summary.AgentRole : role.Name);
            if (summary.Success)
            {
                stream.Push(SessionEventTypes.StreamingDone,
                    payload: JsonSerializer.Serialize(new
                    {
                        role = runtimeRole,
                        roleName = role.Name,
                        model = summary.Model ?? role.ModelName,
                        content = summary.Content,
                        thinking = string.IsNullOrWhiteSpace(summary.Thinking) ? null : summary.Thinking,
                        usage = summary.Usage == null
                            ? null
                            : new
                            {
                                inputTokens = summary.Usage.InputTokens,
                                outputTokens = summary.Usage.OutputTokens,
                                totalTokens = summary.Usage.TotalTokens
                            },
                        timing = summary.Timing == null
                            ? null
                            : new
                            {
                                totalMs = summary.Timing.TotalMs,
                                firstTokenMs = summary.Timing.FirstTokenMs
                            },
                        // 这里的 toolCalls 只会包含“真正被模型当作工具调用过的 AITool”。
                        // 也就是说：
                        // 1. MCP 如果成功挂成 AITool 并且模型真的调用了，才会出现在这里；
                        // 2. Skill 走的是上下文提供器 / 技能目录注入，不会天然出现在这里。
                        toolCalls = summary.ToolCalls.Count == 0
                            ? null
                              : summary.ToolCalls.Select(toolCall => new
                              {
                                  toolCallId = toolCall.CallId,
                                  name = toolCall.Name,
                                 arguments = string.IsNullOrWhiteSpace(toolCall.Arguments) ? null : toolCall.Arguments,
                                 result = string.IsNullOrWhiteSpace(toolCall.Result) ? null : toolCall.Result,
                                 error = string.IsNullOrWhiteSpace(toolCall.Error) ? null : toolCall.Error,
                                 status = toolCall.Status.ToString().ToLowerInvariant()
                             })
                     }));
            }
            else
            {
                var errorText = string.IsNullOrWhiteSpace(summary.Error)
                    ? summary.Cancelled
                        ? "Message execution cancelled."
                        : "Message execution failed."
                    : summary.Error;
                stream.Push(
                    SessionEventTypes.Error,
                    payload: JsonSerializer.Serialize(new
                    {
                        role = runtimeRole,
                        roleName = role.Name,
                        model = summary.Model ?? role.ModelName,
                        message = errorText,
                        error = errorText,
                        cancelled = summary.Cancelled,
                        thinking = string.IsNullOrWhiteSpace(summary.Thinking) ? null : summary.Thinking,
                        usage = summary.Usage == null
                            ? null
                            : new
                            {
                                inputTokens = summary.Usage.InputTokens,
                                outputTokens = summary.Usage.OutputTokens,
                                totalTokens = summary.Usage.TotalTokens
                            },
                        timing = summary.Timing == null
                            ? null
                            : new
                            {
                                totalMs = summary.Timing.TotalMs,
                                firstTokenMs = summary.Timing.FirstTokenMs
                            },
                        // 失败事件同样带回工具调用快照，方便看“模型在报错前到底有没有真正调用工具”。
                        toolCalls = summary.ToolCalls.Count == 0
                            ? null
                            : summary.ToolCalls.Select(toolCall => new
                            {
                                toolCallId = toolCall.CallId,
                                name = toolCall.Name,
                                arguments = string.IsNullOrWhiteSpace(toolCall.Arguments) ? null : toolCall.Arguments,
                                result = string.IsNullOrWhiteSpace(toolCall.Result) ? null : toolCall.Result,
                                error = string.IsNullOrWhiteSpace(toolCall.Error) ? null : toolCall.Error,
                                status = toolCall.Status.ToString().ToLowerInvariant()
                            })
                    }));
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            // 测试对话结束后要主动把内存里的 handler 和瞬态流清掉，
            // 否则前端继续订阅会拿到过期状态，服务端也会残留无用对象。
            _agentService.RemoveMessageHandler(handler.MessageId);
            _streamManager.CompleteTransient(sessionId);
        }
    }

    /// <inheritdoc />
    public async Task<List<VendorModelDto>> GetVendorModelsAsync(string providerType, CancellationToken ct = default)
    {
        if (!_vendorPlatforms.TryGetVendorPlatform(providerType, out var vendorPlatform)
            || vendorPlatform.ModelCatalog is null)
            return [];

        var catalog = await vendorPlatform.ModelCatalog.GetModelCatalogAsync(ct);
        return catalog.Models.Select(m => new VendorModelDto(m.Id, m.Name, m.Family)).ToList();
    }

    /// <inheritdoc />
    public async Task<VendorModelCatalogDto?> GetVendorModelCatalogAsync(string providerType, CancellationToken ct = default)
    {
        if (!_vendorPlatforms.TryGetVendorPlatform(providerType, out var vendorPlatform)
            || vendorPlatform.ModelCatalog is null)
            return null;

        var metadata = vendorPlatform.Metadata;
        var catalog = await vendorPlatform.ModelCatalog.GetModelCatalogAsync(ct);
        return new VendorModelCatalogDto
        {
            ProviderType = metadata.ProviderType,
            Status = MapVendorModelCatalogStatus(catalog.Status),
            Message = catalog.Message,
            MissingConfigurationFields = catalog.MissingConfigurationFields?.ToList() ?? [],
            Models = catalog.Models.Select(m => new VendorModelDto(m.Id, m.Name, m.Family)).ToList()
        };
    }

    /// <inheritdoc />
    public async Task<VendorProviderConfigurationDto?> GetVendorProviderConfigurationAsync(string providerType, CancellationToken ct = default)
    {
        if (!_vendorPlatforms.TryGetVendorPlatform(providerType, out var vendorPlatform))
            return null;

        var metadata = vendorPlatform.Metadata;
        if (vendorPlatform.Configuration is null)
        {
            return new VendorProviderConfigurationDto
            {
                ProviderType = metadata.ProviderType,
                DisplayName = metadata.DisplayName,
                AvatarDataUri = metadata.AvatarDataUri,
                Properties = [],
                Configuration = []
            };
        }

        return new VendorProviderConfigurationDto
        {
            ProviderType = metadata.ProviderType,
            DisplayName = metadata.DisplayName,
            AvatarDataUri = metadata.AvatarDataUri,
            Properties = vendorPlatform.Configuration.ConfigurationProperties
                .Select(MapVendorProviderConfigurationProperty)
                .ToList(),
            Configuration = await vendorPlatform.Configuration.GetConfigurationValuesAsync(ct)
        };
    }

    /// <inheritdoc />
    public async Task<VendorProviderConfigurationDto?> UpdateVendorProviderConfigurationAsync(
        string providerType,
        UpdateVendorProviderConfigurationInput input,
        CancellationToken ct = default)
    {
        if (!_vendorPlatforms.TryGetVendorPlatform(providerType, out var vendorPlatform)
            || vendorPlatform.Configuration is null)
            return null;

        await vendorPlatform.Configuration.SetConfigurationValuesAsync(input.Configuration ?? [], ct);
        return await GetVendorProviderConfigurationAsync(providerType, ct);
    }

    protected override KeyNotFoundException CreateEntityNotFoundException(Guid id) =>
        new($"Agent role '{id}' was not found.");

    protected override AgentRoleDto MapToDto(AgentRole entity) => BuildDto(entity, vendorAvatar: GetVendorAvatar(entity));

    protected override AgentRole MapToEntity(CreateAgentRoleInput input)
    {
        var role = Mapper.ToEntity(input);
        role.Id = Guid.NewGuid();
        role.IsBuiltin = false;
        role.IsActive = true;
        return role;
    }

    protected override AgentRole MapToEntity(UpdateAgentRoleInput input, AgentRole entity)
    {
        ApplyRoleUpdate(input, entity);
        return entity;
    }

    private async Task<Dictionary<Guid, string>> ResolveProviderNamesAsync(IEnumerable<AgentRole> roles)
    {
        var providerNames = new Dictionary<Guid, string>();
        foreach (var role in roles)
        {
            if (role.ModelProviderId.HasValue && !providerNames.ContainsKey(role.ModelProviderId.Value))
            {
                var account = await _accountService.GetByIdAsync(role.ModelProviderId.Value);
                if (account != null)
                    providerNames[role.ModelProviderId.Value] = account.Name;
            }
        }

        return providerNames;
    }

    private async Task<AgentSoulDto?> NormalizeSoulInputAsync(AgentSoulDto? soul)
    {
        if (soul is null)
            return null;

        if (_agentSoulService is null)
        {
            return new AgentSoulDto
            {
                Traits = NormalizeSoulValues(soul.Traits),
                Style = NormalizeSoulValue(soul.Style),
                Attitudes = NormalizeSoulValues(soul.Attitudes),
                Custom = NormalizeSoulValue(soul.Custom)
            };
        }

        return new AgentSoulDto
        {
            Traits = await NormalizeSoulValuesAsync(soul.Traits, _agentSoulService.PersonalityTraits),
            Style = await NormalizeSoulKeyAsync(soul.Style, _agentSoulService.CommunicationStyles),
            Attitudes = await NormalizeSoulValuesAsync(soul.Attitudes, _agentSoulService.WorkAttitudes),
            Custom = NormalizeSoulValue(soul.Custom)
        };
    }

    private static List<string> NormalizeSoulValues(IEnumerable<string>? values)
        => values?
            .Select(NormalizeSoulValue)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
           ?? [];

    private static async Task<List<string>> NormalizeSoulValuesAsync(
        IEnumerable<string>? values,
        IAgentSoulHttpService service)
    {
        var results = new List<string>();
        if (values is null)
            return results;

        foreach (var value in values)
        {
            var normalized = await NormalizeSoulKeyAsync(value, service);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            if (!results.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                results.Add(normalized);
        }

        return results;
    }

    private static async Task<string?> NormalizeSoulKeyAsync(string? value, IAgentSoulHttpService service)
    {
        var normalized = NormalizeSoulValue(value);
        if (normalized is null)
            return null;

        return await service.FindKeyAsync(normalized) ?? normalized;
    }

    private static string? NormalizeSoulValue(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<AgentRoleDto> BuildDtoAsync(AgentRole role)
    {
        var providerName = role.ModelProviderId.HasValue
            ? (await _accountService.GetByIdAsync(role.ModelProviderId.Value))?.Name
            : null;
        return BuildDto(role, providerName, GetVendorAvatar(role));
    }

    private AgentRoleDto BuildDto(AgentRole role, string? providerName = null, string? vendorAvatar = null)
    {
        var dto = Mapper.ToDto(role);
        dto.JobTitle = AgentJobTitleCatalog.NormalizeKey(dto.JobTitle);
        dto.Avatar ??= vendorAvatar;
        dto.ModelProviderName = providerName;
        return dto;
    }

    private static void ApplyRoleUpdate(UpdateAgentRoleInput input, AgentRole role)
    {
        if (input.Name != null)
            role.Name = input.Name;

        if (input.Description != null)
            role.Description = input.Description;

        if (input.JobTitle != null)
            role.JobTitle = AgentJobTitleCatalog.NormalizeKey(input.JobTitle);

        if (input.Avatar != null)
            role.Avatar = input.Avatar;

        if (role.IsBuiltin)
        {
            if (input.ModelProviderId != null)
            {
                if (string.IsNullOrWhiteSpace(input.ModelProviderId))
                {
                    role.ModelProviderId = null;
                }
                else
                {
                    var providerId = Guid.Parse(input.ModelProviderId);
                    role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
                }
            }

            if (input.ModelName != null)
                role.ModelName = input.ModelName;

            if (input.Config != null)
                role.Config = input.Config;

            if (input.Soul != null)
                role.Soul = AgentRoleExecutionProfileFactory.MapSoulFromDto(input.Soul);
        }
        else
        {
            if (input.ModelProviderId != null)
            {
                if (string.IsNullOrWhiteSpace(input.ModelProviderId))
                {
                    role.ModelProviderId = null;
                }
                else
                {
                    var providerId = Guid.Parse(input.ModelProviderId);
                    role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
                }
            }

            if (input.ModelName != null)
                role.ModelName = input.ModelName;

            if (input.Config != null)
                role.Config = input.Config;

            if (input.Soul != null)
                role.Soul = AgentRoleExecutionProfileFactory.MapSoulFromDto(input.Soul);
        }

        if (input.Avatar != null)
            role.Avatar = input.Avatar;
    }

    /// <summary>
    /// 从已注册的 Vendor 平台目录中解析角色对应的默认头像。
    /// Resolves the default avatar for a role from the registered vendor platform catalog.
    /// </summary>
    /// <param name="role">需要解析头像的角色。/ Role whose avatar should be resolved.</param>
    /// <returns>匹配到的头像 Data URI；若为内置角色或无匹配提供器则返回 <see langword="null" />。/ The matching avatar data URI, or <see langword="null" /> for builtin roles or when no provider matches.</returns>
    /// <remarks>
    /// zh-CN: 该查找仅访问进程内的 Vendor 平台目录，不触发网络请求，也不会修改角色配置。
    /// en: This lookup only reads the in-process vendor platform catalog; it does not trigger network calls or mutate role configuration.
    /// </remarks>
    private string? GetVendorAvatar(AgentRole role)
    {
        var providerType = role.ProviderType;
        if (string.IsNullOrEmpty(providerType) || providerType == "builtin") return null;
        return _vendorPlatforms.TryGetVendorPlatform(providerType, out var vendorPlatform)
            ? vendorPlatform.Metadata.AvatarDataUri
            : null;
    }

    private static string MapVendorModelCatalogStatus(VendorModelCatalogStatus status) => status switch
    {
        VendorModelCatalogStatus.Ready => "ready",
        VendorModelCatalogStatus.RequiresProviderConfiguration => "requires_provider_configuration",
        VendorModelCatalogStatus.LoadFailed => "load_failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
    };

    private static VendorProviderConfigurationPropertyDto MapVendorProviderConfigurationProperty(ConfigurationProperty property) => new()
    {
        Name = property.Name,
        FieldType = MapConfigurationPropertyFieldType(property.Type),
        DefaultValue = property.DefaultValue,
        Required = property.Required
    };

    private static string MapConfigurationPropertyFieldType(ConfigurationPropertyType type) => type switch
    {
        ConfigurationPropertyType.Boolean => "boolean",
        ConfigurationPropertyType.Double => "double",
        ConfigurationPropertyType.Int64 => "int64",
        _ => "string"
    };

    private AgentRoleTemplateImportService GetRoleTemplateImportService()
        => _roleTemplateImportService
           ?? throw new InvalidOperationException("Agent role template import service is not available.");

}




