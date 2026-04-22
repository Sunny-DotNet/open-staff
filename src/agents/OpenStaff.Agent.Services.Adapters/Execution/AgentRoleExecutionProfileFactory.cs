using System.Text;
using OpenStaff.Dtos;
using OpenStaff.Entities;

namespace OpenStaff.Agent.Services.Adapters;

/// <summary>
/// zh-CN: 在应用运行时覆盖后构建角色的有效执行画像。
/// en: Builds the effective execution profile for an agent role after applying live overrides.
/// </summary>
public static class AgentRoleExecutionProfileFactory
{
    /// <summary>
    /// zh-CN: 基于持久化角色和临时覆盖创建本次执行使用的角色副本。
    /// en: Creates the role copy used for the current execution from the persisted role and transient overrides.
    /// </summary>
    public static AgentRole CreateEffectiveRole(AgentRole sourceRole, AgentRoleInput? liveOverride)
    {
        // zh-CN: 运行时覆盖只影响当前消息，因此先深拷贝角色，避免污染数据库实体或缓存实例。
        // en: Runtime overrides must affect only the current message, so clone the role first to avoid mutating persisted or cached state.
        var effectiveRole = Clone(sourceRole);

        if (liveOverride == null)
            return effectiveRole;

        if (!string.IsNullOrWhiteSpace(liveOverride.Name))
            effectiveRole.Name = liveOverride.Name;
        if (!string.IsNullOrWhiteSpace(liveOverride.Description))
            effectiveRole.Description = liveOverride.Description;
        if (!string.IsNullOrWhiteSpace(liveOverride.ModelName))
            effectiveRole.ModelName = liveOverride.ModelName;
        if (!string.IsNullOrWhiteSpace(liveOverride.ModelProviderId))
            effectiveRole.ModelProviderId = Guid.Parse(liveOverride.ModelProviderId);
        if (liveOverride.Soul != null)
            effectiveRole.Soul = MapSoulFromDto(liveOverride.Soul);


        return effectiveRole;
    }

    /// <summary>
    /// zh-CN: 将应用层 DTO 中的灵魂配置映射为领域模型。
    /// en: Maps soul configuration from the application DTO into the domain model.
    /// </summary>
    public static AgentSoul? MapSoulFromDto(AgentSoulDto? dto)
    {
        if (dto == null)
            return null;

        return new AgentSoul
        {
            Traits = dto.Traits ?? [],
            Style = dto.Style,
            Attitudes = dto.Attitudes ?? [],
            Custom = dto.Custom
        };
    }

    /// <summary>
    /// zh-CN: 根据角色的自定义身份信息生成轻量系统提示词。
    /// en: Builds a lightweight system prompt from the role's custom identity data.
    /// </summary>
    public static string BuildSystemPrompt(AgentRole role)
    {
        var builder = new StringBuilder();
        builder.AppendLine("以下是你的身份信息");

        if (!string.IsNullOrEmpty(role.Name))
            builder.AppendLine($"名称:```{role.Name}```");
        if (!string.IsNullOrEmpty(role.Description))
            builder.AppendLine($"职务说明:```{role.Description}```");

        if (role.Soul != null)
        {
            if (role.Soul.Traits.Count > 0)
                builder.AppendLine($"性格特征:{string.Join(',', role.Soul.Traits.Select(trait => $"```{trait}```"))}");
            if (!string.IsNullOrEmpty(role.Soul.Style))
                builder.AppendLine($"沟通风格:```{role.Soul.Style}```");
            if (role.Soul.Attitudes.Count > 0)
                builder.AppendLine($"工作态度:{string.Join(',', role.Soul.Attitudes.Select(attitude => $"```{attitude}```"))}");
            if (!string.IsNullOrEmpty(role.Soul.Custom))
                builder.AppendLine($"其它补充:```{role.Soul.Custom}```");
        }

        return builder.ToString();
    }

    /// <summary>
    /// zh-CN: 深拷贝运行时会修改的角色字段，确保临时覆盖不会回写到持久化实体或共享缓存实例。
    /// en: Deep-clones the role fields that may be mutated at runtime so transient overrides never leak back into persisted entities or shared cached instances.
    /// </summary>
    private static AgentRole Clone(AgentRole sourceRole)
    {
        return new AgentRole
        {
            Id = sourceRole.Id,
            Name = sourceRole.Name,
            Description = sourceRole.Description,
            JobTitle = sourceRole.JobTitle,
            Avatar = sourceRole.Avatar,
            ModelProviderId = sourceRole.ModelProviderId,
            ModelName = sourceRole.ModelName,
            Source = sourceRole.Source,
            ProviderType = sourceRole.ProviderType,
            IsBuiltin = sourceRole.IsBuiltin,
            IsActive = sourceRole.IsActive,
            PluginId = sourceRole.PluginId,
            Config = sourceRole.Config,
            Soul = sourceRole.Soul == null
                ? null
                : new AgentSoul
                {
                    Traits = sourceRole.Soul.Traits.ToList(),
                    Style = sourceRole.Soul.Style,
                    Attitudes = sourceRole.Soul.Attitudes.ToList(),
                    Custom = sourceRole.Soul.Custom
                },
            CreatedAt = sourceRole.CreatedAt,
            UpdatedAt = sourceRole.UpdatedAt
        };
    }
}
