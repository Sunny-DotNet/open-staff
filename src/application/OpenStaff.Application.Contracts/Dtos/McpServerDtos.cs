using OpenStaff.Entities;

namespace OpenStaff.Dtos;

/// <summary>
/// MCP 服务定义摘要。
/// Summary information for an MCP server definition.
/// </summary>
public class McpServerDto
{
    /// <summary>服务唯一标识。 / Unique server identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>服务名称。 / Server name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>服务描述。 / Server description.</summary>
    public string? Description { get; set; }

    /// <summary>图标或图标数据。 / Icon or icon payload.</summary>
    public string? Icon { get; set; }

    /// <summary>品牌 Logo 标识。 / Brand-logo identifier.</summary>
    public string? Logo { get; set; }

    /// <summary>服务分类。 / Server category.</summary>
    public string Category { get; set; } = "general";

    /// <summary>默认传输方式，例如 stdio、sse 或 http。 / Default transport type such as stdio, sse, or http.</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>安装模式，例如 local 或 remote。 / Installation mode such as local or remote.</summary>
    public string Mode { get; set; } = "local";

    /// <summary>服务来源，例如 builtin 或 marketplace。 / Server source such as builtin or marketplace.</summary>
    public string Source { get; set; } = "builtin";

    /// <summary>结构化模板 JSON。 / Structured template JSON.</summary>
    public string? TemplateJson { get; set; }

    /// <summary>受管安装标识。 / Managed-install identifier.</summary>
    public Guid? InstallId { get; set; }

    /// <summary>目录条目标识。 / Catalog-entry identifier.</summary>
    public string? CatalogEntryId { get; set; }

    /// <summary>安装来源键。 / Install source key.</summary>
    public string? InstallSourceKey { get; set; }

    /// <summary>安装通道标识。 / Install channel identifier.</summary>
    public string? InstallChannelId { get; set; }

    /// <summary>安装通道类型。 / Install channel type.</summary>
    public string? InstallChannelType { get; set; }

    /// <summary>已安装版本。 / Installed version.</summary>
    public string? InstalledVersion { get; set; }

    /// <summary>安装状态。 / Install state.</summary>
    public string? InstalledState { get; set; }

    /// <summary>安装目录。 / Install directory.</summary>
    public string? InstallDirectory { get; set; }

    /// <summary>Manifest 路径。 / Manifest path.</summary>
    public string? ManifestPath { get; set; }

    /// <summary>最近一次安装错误。 / Last install error.</summary>
    public string? LastInstallError { get; set; }

    /// <summary>是否为 OpenStaff 受管安装。 / Whether the server is backed by a managed OpenStaff install.</summary>
    public bool IsManagedInstall { get; set; }

    /// <summary>主页地址。 / Homepage URL.</summary>
    public string? Homepage { get; set; }

    /// <summary>NPM 包名称。 / NPM package name.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>PyPI 包名称。 / PyPI package name.</summary>
    public string? PypiPackage { get; set; }

    /// <summary>是否启用。 / Whether the definition is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>已创建的配置实例数量。 / Number of configuration instances already created.</summary>
    public int ConfigCount { get; set; }

    /// <summary>默认执行档案标识。 / Preferred launch-profile identifier.</summary>
    public string? DefaultProfileId { get; set; }

    /// <summary>结构化执行档案。 / Structured launch profiles.</summary>
    public List<McpLaunchProfileDto> Profiles { get; set; } = [];

    /// <summary>结构化参数 Schema。 / Structured parameter schema.</summary>
    public List<McpParameterSchemaItemDto> ParameterSchema { get; set; } = [];
}

/// <summary>
/// MCP 结构化执行档案。
/// Structured launch profile for an MCP capability.
/// </summary>
public class McpLaunchProfileDto
{
    /// <summary>档案唯一标识。 / Profile identifier.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>显示名称。 / Human-readable display name.</summary>
    public string? DisplayName { get; set; }

    /// <summary>档案类型。 / Profile type such as package, remote, container, or binary.</summary>
    public string ProfileType { get; set; } = string.Empty;

    /// <summary>传输类型。 / Transport type.</summary>
    public string TransportType { get; set; } = McpTransportTypes.Stdio;

    /// <summary>运行器类别。 / Runner kind such as package, remote, container, or binary.</summary>
    public string? RunnerKind { get; set; }

    /// <summary>运行器标识。 / Concrete runner identifier such as npx, uvx, docker, or remote.</summary>
    public string? Runner { get; set; }

    /// <summary>生态标识。 / Ecosystem such as npm or python.</summary>
    public string? Ecosystem { get; set; }

    /// <summary>包名称。 / Package name.</summary>
    public string? PackageName { get; set; }

    /// <summary>包版本。 / Package version.</summary>
    public string? PackageVersion { get; set; }

    /// <summary>镜像名称。 / Container image.</summary>
    public string? Image { get; set; }

    /// <summary>镜像标签模板。 / Container image-tag template.</summary>
    public string? ImageTagTemplate { get; set; }

    /// <summary>固定命令。 / Fixed command.</summary>
    public string? Command { get; set; }

    /// <summary>命令模板。 / Command template.</summary>
    public string? CommandTemplate { get; set; }

    /// <summary>工作目录模板。 / Working-directory template.</summary>
    public string? WorkingDirectoryTemplate { get; set; }

    /// <summary>远程 URL 模板。 / Remote URL template.</summary>
    public string? UrlTemplate { get; set; }

    /// <summary>参数模板。 / Argument template.</summary>
    public List<string> ArgsTemplate { get; set; } = [];

    /// <summary>环境变量模板。 / Environment-variable template.</summary>
    public Dictionary<string, string?> EnvTemplate { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>请求头模板。 / Header template.</summary>
    public Dictionary<string, string?> HeadersTemplate { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// MCP 参数 Schema 项。
/// Parameter-schema item for an MCP capability.
/// </summary>
public class McpParameterSchemaItemDto
{
    /// <summary>参数键。 / Parameter key.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>参数标签。 / Human-readable label.</summary>
    public string? Label { get; set; }

    /// <summary>参数类型。 / Parameter type such as string, password, or boolean.</summary>
    public string Type { get; set; } = "string";

    /// <summary>是否必填。 / Whether the parameter is required.</summary>
    public bool Required { get; set; }

    /// <summary>默认值。 / Default value.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>默认值来源。 / Default-value source descriptor.</summary>
    public string? DefaultValueSource { get; set; }

    /// <summary>项目上下文覆盖来源。 / Project-level override source descriptor.</summary>
    public string? ProjectOverrideValueSource { get; set; }

    /// <summary>说明。 / Parameter description.</summary>
    public string? Description { get; set; }

    /// <summary>适用档案列表。 / Profiles that the parameter applies to.</summary>
    public List<string> AppliesToProfiles { get; set; } = [];
}

/// <summary>
/// MCP 服务配置实例。
/// Configuration instance for an MCP server.
/// </summary>
public class McpServerConfigDto
{
    /// <summary>配置唯一标识。 / Unique configuration identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>所属服务定义标识。 / Owning server definition identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>所属服务名称。 / Owning server name.</summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>配置名称。 / Configuration name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置描述。 / Configuration description.</summary>
    public string? Description { get; set; }

    /// <summary>传输方式。 / Transport type.</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否存在已保存的环境变量。 / Whether saved environment variables exist.</summary>
    public bool HasEnvironmentVariables { get; set; }

    /// <summary>是否存在已保存的认证配置。 / Whether saved auth configuration exists.</summary>
    public bool HasAuthConfig { get; set; }

    /// <summary>是否启用。 / Whether the configuration is enabled.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>创建时间（UTC）。 / Creation time in UTC.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 创建 MCP 服务定义的输入参数。
/// Input used to create an MCP server definition.
/// </summary>
public class CreateMcpServerInput
{
    /// <summary>服务名称。 / Server name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>服务描述。 / Server description.</summary>
    public string? Description { get; set; }

    /// <summary>图标或图标数据。 / Icon or icon payload.</summary>
    public string? Icon { get; set; }

    /// <summary>服务分类。 / Server category.</summary>
    public string Category { get; set; } = "general";

    /// <summary>默认传输方式。 / Default transport type.</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>安装模式。 / Installation mode.</summary>
    public string Mode { get; set; } = "local";

    /// <summary>结构化模板 JSON。 / Structured template JSON.</summary>
    public string? TemplateJson { get; set; }

    /// <summary>主页地址。 / Homepage URL.</summary>
    public string? Homepage { get; set; }

    /// <summary>NPM 包名称。 / NPM package name.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>PyPI 包名称。 / PyPI package name.</summary>
    public string? PypiPackage { get; set; }
}

/// <summary>
/// 更新 MCP 服务定义的输入参数。
/// Input used to update an MCP server definition.
/// </summary>
public class UpdateMcpServerInput
{
    /// <summary>服务名称。 / Server name.</summary>
    public string? Name { get; set; }

    /// <summary>服务描述。 / Server description.</summary>
    public string? Description { get; set; }

    /// <summary>图标或图标数据。 / Icon or icon payload.</summary>
    public string? Icon { get; set; }

    /// <summary>服务分类。 / Server category.</summary>
    public string? Category { get; set; }

    /// <summary>默认传输方式。 / Default transport type.</summary>
    public string? TransportType { get; set; }

    /// <summary>安装模式。 / Installation mode.</summary>
    public string? Mode { get; set; }

    /// <summary>结构化模板 JSON。 / Structured template JSON.</summary>
    public string? TemplateJson { get; set; }

    /// <summary>主页地址。 / Homepage URL.</summary>
    public string? Homepage { get; set; }

    /// <summary>NPM 包名称。 / NPM package name.</summary>
    public string? NpmPackage { get; set; }

    /// <summary>PyPI 包名称。 / PyPI package name.</summary>
    public string? PypiPackage { get; set; }

    /// <summary>是否启用。 / Whether the definition is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// 创建 MCP 配置实例的输入参数。
/// Input used to create an MCP configuration instance.
/// </summary>
public class CreateMcpServerConfigInput
{
    /// <summary>服务定义标识。 / Server definition identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>配置名称。 / Configuration name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>配置描述。 / Configuration description.</summary>
    public string? Description { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }
}

/// <summary>
/// 更新 MCP 配置实例的输入参数。
/// Input used to update an MCP configuration instance.
/// </summary>
public class UpdateMcpServerConfigInput
{
    /// <summary>配置名称。 / Configuration name.</summary>
    public string? Name { get; set; }

    /// <summary>配置描述。 / Configuration description.</summary>
    public string? Description { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the configuration is enabled.</summary>
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// MCP 工具定义。
/// Tool definition exposed by an MCP server.
/// </summary>
public class McpToolDto
{
    /// <summary>工具名称。 / Tool name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>工具描述。 / Tool description.</summary>
    public string? Description { get; set; }

    /// <summary>输入架构 JSON。 / Input schema JSON.</summary>
    public string? InputSchema { get; set; }
}

/// <summary>
/// MCP 连通性测试结果。
/// Result of testing an MCP configuration connection.
/// </summary>
public class TestMcpConnectionResult
{
    /// <summary>连接是否成功。 / Whether the connection succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>测试结果消息。 / Test result message.</summary>
    public string? Message { get; set; }

    /// <summary>发现的工具列表。 / Tools discovered during the test.</summary>
    public List<McpToolDto> Tools { get; set; } = [];
}

/// <summary>
/// 角色测试场景与已安装 MCP 服务器之间的绑定信息。
/// Binding information between an agent-role test chat and an installed MCP server.
/// </summary>
public class AgentRoleMcpBindingDto
{
    /// <summary>绑定唯一标识。 / Unique binding identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>角色标识。 / Agent-role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>绑定的 MCP 服务器标识。 / Bound MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>服务名称。 / Server name.</summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>图标或图标数据。 / Icon or icon payload.</summary>
    public string? Icon { get; set; }

    /// <summary>服务模式。 / Server mode.</summary>
    public string Mode { get; set; } = McpServerModes.Local;

    /// <summary>传输类型。 / Transport type.</summary>
    public string TransportType { get; set; } = McpTransportTypes.Stdio;

    /// <summary>工具过滤表达式。 / Tool filter expression.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 项目智能体与 MCP 服务器之间的绑定信息。
/// Binding information between a project agent and an installed MCP server.
/// </summary>
public class ProjectAgentMcpBindingDto
{
    /// <summary>绑定唯一标识。 / Unique binding identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>项目智能体标识。 / Project-agent identifier.</summary>
    public Guid ProjectAgentRoleId { get; set; }

    /// <summary>绑定的 MCP 服务器标识。 / Bound MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>服务名称。 / Server name.</summary>
    public string McpServerName { get; set; } = string.Empty;

    /// <summary>服务图标。 / Server icon.</summary>
    public string? Icon { get; set; }

    /// <summary>服务模式。 / Server mode.</summary>
    public string Mode { get; set; } = "local";

    /// <summary>传输类型。 / Transport type.</summary>
    public string TransportType { get; set; } = "stdio";

    /// <summary>工具过滤表达式。 / Tool filter expression.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// 查询 MCP 服务定义列表的请求。
/// Request used to query MCP server definitions.
/// </summary>
public class GetAllServersRequest
{
    /// <summary>来源过滤条件。 / Source filter.</summary>
    public string? Source { get; set; }

    /// <summary>分类过滤条件。 / Category filter.</summary>
    public string? Category { get; set; }

    /// <summary>关键字搜索词。 / Keyword search term.</summary>
    public string? Search { get; set; }

    /// <summary>启用状态过滤。 / Enabled-state filter.</summary>
    public bool? EnabledState { get; set; }

    /// <summary>安装状态过滤。 / Install-state filter.</summary>
    public string? InstalledState { get; set; }
}

/// <summary>
/// 替换角色测试 MCP 绑定列表的请求。
/// Request used to replace the MCP bindings for an agent-role test chat.
/// </summary>
public class ReplaceAgentRoleMcpBindingsRequest
{
    /// <summary>角色标识。 / Agent role identifier.</summary>
    public Guid AgentRoleId { get; set; }

    /// <summary>绑定列表。 / Binding list.</summary>
    public List<AgentRoleMcpBindingInput> Bindings { get; set; } = [];
}

/// <summary>
/// 角色测试 MCP 绑定输入参数。
/// Input used to create or replace an agent-role test MCP binding.
/// </summary>
public class AgentRoleMcpBindingInput
{
    /// <summary>MCP 服务器标识。 / MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>工具过滤表达式。 / Tool filter expression.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// 替换项目智能体 MCP 绑定列表的请求。
/// Request used to replace the MCP bindings for a project agent.
/// </summary>
public class ReplaceProjectAgentMcpBindingsRequest
{
    /// <summary>项目智能体标识。 / Project-agent identifier.</summary>
    public Guid ProjectAgentRoleId { get; set; }

    /// <summary>绑定列表。 / Binding list.</summary>
    public List<ProjectAgentMcpBindingInput> Bindings { get; set; } = [];
}

/// <summary>
/// 项目智能体 MCP 绑定输入参数。
/// Input used to create or replace a project-agent MCP binding.
/// </summary>
public class ProjectAgentMcpBindingInput
{
    /// <summary>MCP 服务器标识。 / MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>工具过滤表达式。 / Tool filter expression.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the binding is enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// MCP 绑定默认参数草稿作用域常量。
/// Well-known scopes used when generating MCP binding drafts.
/// </summary>
public static class McpBindingDraftScopes
{
    public const string AgentRole = "agent-role";
    public const string AgentRoleTest = "agent-role-test";
    public const string ProjectAgentRole = "project-agent";
}

/// <summary>
/// 请求生成某个场景下的 MCP 绑定草稿。
/// Request used to generate an MCP binding draft for a specific scope.
/// </summary>
public class CreateMcpBindingDraftInput
{
    /// <summary>MCP 服务器标识。 / MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>目标作用域。 / Target binding scope.</summary>
    public string Scope { get; set; } = McpBindingDraftScopes.ProjectAgentRole;

    /// <summary>项目智能体标识。 / Project-agent identifier.</summary>
    public Guid? ProjectAgentRoleId { get; set; }

    /// <summary>角色标识。 / Agent-role identifier.</summary>
    public Guid? AgentRoleId { get; set; }
}

/// <summary>
/// MCP 绑定草稿结果。
/// Generated MCP binding draft returned to clients before persistence.
/// </summary>
public class McpBindingDraftDto
{
    /// <summary>MCP 服务器标识。 / MCP server identifier.</summary>
    public Guid McpServerId { get; set; }

    /// <summary>工具过滤表达式。 / Tool filter expression.</summary>
    public string? ToolFilter { get; set; }

    /// <summary>选中的执行档案。 / Selected launch profile identifier.</summary>
    public string? SelectedProfileId { get; set; }

    /// <summary>参数值 JSON。 / Parameter-value JSON.</summary>
    public string? ParameterValues { get; set; }

    /// <summary>是否启用。 / Whether the binding should start enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}
