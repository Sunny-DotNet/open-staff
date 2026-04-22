using OpenStaff.Agent.Services.Adapters;
using OpenStaff.Dtos;
using OpenStaff.Entities;
using Riok.Mapperly.Abstractions;

namespace OpenStaff.Application.AgentRoles.Services;
[Mapper]
public partial class AgentRoleMapper
{
    [MapperIgnoreSource(nameof(AgentRole.IsActive))]
    [MapperIgnoreSource(nameof(AgentRole.PluginId))]
    [MapperIgnoreSource(nameof(AgentRole.UpdatedAt))]
    [MapperIgnoreSource(nameof(AgentRole.Plugin))]
    [MapperIgnoreSource(nameof(AgentRole.ProjectAgentRoles))]
    [MapperIgnoreSource(nameof(AgentRole.McpBindings))]
    [MapperIgnoreSource(nameof(AgentRole.SkillBindings))]
    [MapperIgnoreTarget(nameof(AgentRoleDto.IsVirtual))]
    [MapperIgnoreTarget(nameof(AgentRoleDto.ModelProviderName))]
    public partial AgentRoleDto ToDto(AgentRole source);

    [MapperIgnoreSource(nameof(CreateAgentRoleInput.SystemPrompt))]
    [MapperIgnoreTarget(nameof(AgentRole.Id))]
    [MapperIgnoreTarget(nameof(AgentRole.IsBuiltin))]
    [MapperIgnoreTarget(nameof(AgentRole.IsActive))]
    [MapperIgnoreTarget(nameof(AgentRole.CreatedAt))]
    [MapperIgnoreTarget(nameof(AgentRole.UpdatedAt))]
    [MapperIgnoreTarget(nameof(AgentRole.PluginId))]
    [MapperIgnoreTarget(nameof(AgentRole.Plugin))]
    [MapperIgnoreTarget(nameof(AgentRole.ProjectAgentRoles))]
    [MapperIgnoreTarget(nameof(AgentRole.McpBindings))]
    [MapperIgnoreTarget(nameof(AgentRole.SkillBindings))]
    public partial AgentRole ToEntity(CreateAgentRoleInput source);

    private static string? MapModelProviderId(Guid? value) => value?.ToString();

    private static Guid? MapModelProviderId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Guid.Parse(value);

    private static AgentSoulDto? MapSoul(AgentSoul? soul)
    {
        if (soul is null)
            return null;

        return new AgentSoulDto
        {
            Traits = soul.Traits,
            Style = soul.Style,
            Attitudes = soul.Attitudes,
            Custom = soul.Custom
        };
    }

    private static AgentSoul? MapSoul(AgentSoulDto? soul) =>
        soul is null ? null : AgentRoleExecutionProfileFactory.MapSoulFromDto(soul);
}

