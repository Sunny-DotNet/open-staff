using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenStaff.Agents;
using OpenStaff.Application.Contracts.AgentRoles;
using OpenStaff.Application.Contracts.AgentRoles.Dtos;
using OpenStaff.Application.Providers;
using OpenStaff.Application.Sessions;
using OpenStaff.Core.Agents;
using OpenStaff.Core.Models;
using OpenStaff.Infrastructure.Persistence;

namespace OpenStaff.Application.AgentRoles;

public class AgentRoleAppService : IAgentRoleAppService
{
    private readonly AppDbContext _db;
    private readonly ProviderAccountService _accountService;
    private readonly AgentFactory _agentFactory;
    private readonly IProviderResolver _providerResolver;
    private readonly SessionStreamManager _streamManager;

    public AgentRoleAppService(
        AppDbContext db,
        ProviderAccountService accountService,
        AgentFactory agentFactory,
        IProviderResolver providerResolver,
        SessionStreamManager streamManager)
    {
        _db = db;
        _accountService = accountService;
        _agentFactory = agentFactory;
        _providerResolver = providerResolver;
        _streamManager = streamManager;
    }

    public async Task<List<AgentRoleDto>> GetAllAsync(CancellationToken ct)
    {
        var roles = await _db.AgentRoles
            .Where(r => r.IsActive)
            .OrderBy(r => r.IsBuiltin ? 0 : 1)
            .ThenBy(r => r.Name)
            .ToListAsync(ct);

        foreach (var role in roles)
        {
            if (role.ModelProviderId.HasValue)
                role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);
        }

        return roles.Select(MapToDto).ToList();
    }

    public async Task<AgentRoleDto?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var role = await _db.AgentRoles
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) return null;

        if (role.ModelProviderId.HasValue)
            role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);

        return MapToDto(role);
    }

    public async Task<AgentRoleDto> CreateAsync(CreateAgentRoleInput input, CancellationToken ct)
    {
        var role = new AgentRole
        {
            Id = Guid.NewGuid(),
            Name = input.Name,
            RoleType = input.RoleType,
            Description = input.Description,
            SystemPrompt = input.SystemPrompt,
            ModelProviderId = string.IsNullOrEmpty(input.ModelProviderId) ? null : Guid.Parse(input.ModelProviderId),
            ModelName = input.ModelName,
            Config = input.Config,
            IsBuiltin = false,
            IsActive = true
        };

        _db.AgentRoles.Add(role);
        await _db.SaveChangesAsync(ct);
        return MapToDto(role);
    }

    public async Task<AgentRoleDto?> UpdateAsync(Guid id, UpdateAgentRoleInput input, CancellationToken ct)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return null;

        if (role.IsBuiltin)
        {
            if (!string.IsNullOrEmpty(input.ModelProviderId))
            {
                var providerId = Guid.Parse(input.ModelProviderId);
                role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
            }
            if (input.ModelName != null) role.ModelName = input.ModelName;
            if (input.Config != null) role.Config = input.Config;
        }
        else
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("以下是你的身份信息");

            if (input.Name != null)
            {
                role.Name = input.Name;
                stringBuilder.AppendLine($"名称:```{role.Name}```");
            }
            if (input.Description != null)
            {
                role.Description = input.Description;
                stringBuilder.AppendLine($"职务说明:```{role.Description}```");
            }
            if (!string.IsNullOrEmpty(input.ModelProviderId))
            {
                var providerId = Guid.Parse(input.ModelProviderId);
                role.ModelProviderId = providerId == Guid.Empty ? null : providerId;
            }
            if (input.ModelName != null) role.ModelName = input.ModelName;

            if (!string.IsNullOrEmpty(input.Config))
            {
                try
                {
                    role.Config = input.Config;
                    using var doc = JsonDocument.Parse(input.Config);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("soul", out var soul))
                    {
                        if (soul.TryGetProperty("traits", out var traits) && traits.ValueKind == JsonValueKind.Array)
                        {
                            var traitString = string.Join(',', traits.EnumerateArray().Select(x => $"```{x.GetString()}```"));
                            stringBuilder.AppendLine($"性格特征:{traitString}");
                        }
                        if (soul.TryGetProperty("style", out var style) && style.ValueKind == JsonValueKind.String)
                        {
                            stringBuilder.AppendLine($"沟通风格:```{style}```");
                        }
                        if (soul.TryGetProperty("attitudes", out var attitudes) && attitudes.ValueKind == JsonValueKind.Array)
                        {
                            var attitudeString = string.Join(',', attitudes.EnumerateArray().Select(x => $"```{x.GetString()}```"));
                            stringBuilder.AppendLine($"工作态度:{attitudeString}");
                        }
                        if (soul.TryGetProperty("custom", out var custom) && custom.ValueKind == JsonValueKind.String)
                        {
                            stringBuilder.AppendLine($"其它补充:```{custom}```");
                        }
                    }
                }
                catch { /* ignore parse errors */ }
            }
            role.SystemPrompt = stringBuilder.ToString();
        }

        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        _agentFactory.RegisterDbRole(role);

        if (role.ModelProviderId.HasValue)
            role.ProviderAccount = await _accountService.GetByIdAsync(role.ModelProviderId.Value);
        return MapToDto(role);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var role = await _db.AgentRoles.FindAsync(new object[] { id }, ct);
        if (role == null) return false;
        if (role.IsBuiltin) throw new InvalidOperationException("不能删除内置角色");

        role.IsActive = false;
        role.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Guid> TestChatAsync(Guid id, string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message is required");

        var role = await _db.AgentRoles.FirstOrDefaultAsync(r => r.Id == id && r.IsActive, ct);
        if (role == null) throw new KeyNotFoundException("Agent role not found");

        ProviderAccount? account = null;
        string? apiKey = null;
        string? endpointOverride = null;

        if (role.ModelProviderId.HasValue)
        {
            var resolved = await _providerResolver.ResolveAsync(role.ModelProviderId.Value, ct);
            if (resolved != null)
            {
                account = resolved.Account;
                apiKey = resolved.ApiKey;
                endpointOverride = resolved.EndpointOverride;
            }
        }

        if (account == null || string.IsNullOrEmpty(apiKey))
        {
            throw new InvalidOperationException(account == null
                ? "请先在角色配置中选择模型供应商"
                : "请先在设置页面配置供应商的 API Key");
        }

        var sessionId = Guid.NewGuid();
        var stream = _streamManager.Create(sessionId);

        stream.Push(SessionEventTypes.UserInput, payload: JsonSerializer.Serialize(new { content = message }));

        _ = Task.Run(async () =>
        {
            try
            {
                var agent = _agentFactory.CreateAgentFromDbRole(role);

                var context = new AgentContext
                {
                    ProjectId = Guid.Empty,
                    AgentInstanceId = Guid.NewGuid(),
                    Role = new AgentRole
                    {
                        RoleType = role.RoleType,
                        Name = role.Name,
                        ModelName = role.ModelName
                    },
                    Account = account,
                    ApiKey = apiKey,
                    Language = "zh-CN"
                };

                if (endpointOverride != null)
                    context.ExtraConfig["EndpointOverride"] = endpointOverride;

                await agent.InitializeAsync(context);

                var agentMessage = new AgentMessage
                {
                    Content = message,
                    FromRole = "user",
                    Timestamp = DateTime.UtcNow
                };

                var response = await agent.ProcessAsync(agentMessage, CancellationToken.None);

                stream.Push(SessionEventTypes.Message, payload: JsonSerializer.Serialize(new
                {
                    role = role.RoleType,
                    roleName = role.Name,
                    content = response.Content,
                    success = response.Success,
                    targetRole = response.TargetRole,
                    errors = response.Errors
                }));

                _streamManager.CompleteTransient(sessionId);
            }
            catch (Exception ex)
            {
                stream.Push(SessionEventTypes.Error, payload: JsonSerializer.Serialize(new
                {
                    error = ex.Message
                }));
                _streamManager.CompleteTransient(sessionId);
            }
        });

        return sessionId;
    }

    private static AgentRoleDto MapToDto(AgentRole role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        RoleType = role.RoleType,
        Description = role.Description,
        SystemPrompt = role.SystemPrompt,
        ModelProviderId = role.ModelProviderId?.ToString(),
        ModelProviderName = role.ProviderAccount?.Name,
        ModelName = role.ModelName,
        IsBuiltin = role.IsBuiltin,
        Config = role.Config,
        CreatedAt = role.CreatedAt
    };
}
