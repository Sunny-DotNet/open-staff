using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "global_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 255, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_global_settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpServers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Icon = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TransportType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DefaultConfig = table.Column<string>(type: "text", nullable: true),
                    InstallInfo = table.Column<string>(type: "text", nullable: true),
                    MarketplaceUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Homepage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NpmPackage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    PypiPackage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "plugins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Version = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Manifest = table.Column<string>(type: "TEXT", nullable: false),
                    AssemblyPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plugins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "zh-CN"),
                    WorkspacePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "initializing"),
                    Phase = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "brainstorming"),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    DefaultProviderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DefaultModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExtraConfig = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProtocolType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "McpServerConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    TransportType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ConnectionConfig = table.Column<string>(type: "text", nullable: true),
                    EnvironmentVariables = table.Column<string>(type: "text", nullable: true),
                    AuthConfig = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_McpServerConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_McpServerConfigs_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    JobTitle = table.Column<string>(type: "TEXT", nullable: true),
                    Avatar = table.Column<string>(type: "TEXT", nullable: true),
                    ModelProviderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    IsBuiltin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PluginId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Config = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Soul = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_roles_plugins_PluginId",
                        column: x => x.PluginId,
                        principalTable: "plugins",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ChatSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    InitialInput = table.Column<string>(type: "TEXT", nullable: false),
                    FinalResult = table.Column<string>(type: "TEXT", nullable: true),
                    ContextStrategy = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "full"),
                    Scene = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "ProjectBrainstorm"),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatSessions_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "InstalledSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InstallKey = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Scope = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Repo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SkillId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    GithubUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Installs = table.Column<int>(type: "INTEGER", nullable: false),
                    InstallMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TargetAgentsJson = table.Column<string>(type: "text", nullable: false),
                    InstallRootPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    RawMetadataJson = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstalledSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstalledSkills_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRoleMcpBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolFilter = table.Column<string>(type: "text", nullable: true),
                    RuntimeParameters = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRoleMcpBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRoleMcpBindings_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentRoleMcpBindings_agent_roles_AgentRoleId",
                        column: x => x.AgentRoleId,
                        principalTable: "agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRoleMcpConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolFilter = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRoleMcpConfigs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRoleMcpConfigs_McpServerConfigs_McpServerConfigId",
                        column: x => x.McpServerConfigId,
                        principalTable: "McpServerConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AgentRoleMcpConfigs_agent_roles_AgentRoleId",
                        column: x => x.AgentRoleId,
                        principalTable: "agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentRoleSkillBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillInstallKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SkillId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Repo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GithubUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRoleSkillBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentRoleSkillBindings_agent_roles_AgentRoleId",
                        column: x => x.AgentRoleId,
                        principalTable: "agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "idle"),
                    CurrentTask = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_agents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_agents_agent_roles_AgentRoleId",
                        column: x => x.AgentRoleId,
                        principalTable: "agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_agents_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "agent_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    ParentEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_events_agent_events_ParentEventId",
                        column: x => x.ParentEventId,
                        principalTable: "agent_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_events_project_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "project_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_agent_events_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentMcpBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectAgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolFilter = table.Column<string>(type: "text", nullable: true),
                    RuntimeParameters = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentMcpBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentMcpBindings_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAgentMcpBindings_project_agents_ProjectAgentId",
                        column: x => x.ProjectAgentId,
                        principalTable: "project_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentSkillBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectAgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SkillInstallKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SkillId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Repo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    GithubUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentSkillBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentSkillBindings_project_agents_ProjectAgentId",
                        column: x => x.ProjectAgentId,
                        principalTable: "project_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "pending"),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    AssignedAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParentTaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tasks_project_agents_AssignedAgentId",
                        column: x => x.AssignedAgentId,
                        principalTable: "project_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_tasks_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_tasks_tasks_ParentTaskId",
                        column: x => x.ParentTaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChatFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Depth = table.Column<int>(type: "INTEGER", nullable: false),
                    InitiatorRole = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetRole = table.Column<string>(type: "TEXT", nullable: true),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Result = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatFrames_ChatFrames_ParentFrameId",
                        column: x => x.ParentFrameId,
                        principalTable: "ChatFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatFrames_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatFrames_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "checkpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    GitCommitSha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    DiffSummary = table.Column<string>(type: "TEXT", nullable: true),
                    FilesChanged = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_checkpoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_checkpoints_project_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "project_agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_checkpoints_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_checkpoints_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "task_dependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_DependsOnId",
                        column: x => x.DependsOnId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_dependencies_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentMessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Role = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    AgentRole = table.Column<string>(type: "TEXT", nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    ContentType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false, defaultValue: "text"),
                    SequenceNo = table.Column<int>(type: "INTEGER", nullable: false),
                    TokenUsage = table.Column<string>(type: "TEXT", nullable: true),
                    DurationMs = table.Column<long>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatFrames_FrameId",
                        column: x => x.FrameId,
                        principalTable: "ChatFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatMessages_ParentMessageId",
                        column: x => x.ParentMessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ChatMessages_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SessionEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MessageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EventType = table.Column<string>(type: "TEXT", maxLength: 30, nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    SequenceNo = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionEvents_ChatFrames_FrameId",
                        column: x => x.FrameId,
                        principalTable: "ChatFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionEvents_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionEvents_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_events_AgentId_CreatedAt",
                table: "agent_events",
                columns: new[] { "AgentId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_agent_events_ParentEventId",
                table: "agent_events",
                column: "ParentEventId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_events_ProjectId_CreatedAt",
                table: "agent_events",
                columns: new[] { "ProjectId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_AgentEvents_CreatedAt",
                table: "agent_events",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AgentEvents_ProjectId_AgentId",
                table: "agent_events",
                columns: new[] { "ProjectId", "AgentId" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_roles_PluginId",
                table: "agent_roles",
                column: "PluginId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpBindings_AgentRoleId_McpServerId",
                table: "AgentRoleMcpBindings",
                columns: new[] { "AgentRoleId", "McpServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpBindings_IsEnabled",
                table: "AgentRoleMcpBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpBindings_McpServerId",
                table: "AgentRoleMcpBindings",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpConfigs_AgentRoleId_McpServerConfigId",
                table: "AgentRoleMcpConfigs",
                columns: new[] { "AgentRoleId", "McpServerConfigId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpConfigs_McpServerConfigId",
                table: "AgentRoleMcpConfigs",
                column: "McpServerConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleSkillBindings_AgentRoleId_SkillInstallKey",
                table: "AgentRoleSkillBindings",
                columns: new[] { "AgentRoleId", "SkillInstallKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleSkillBindings_IsEnabled",
                table: "AgentRoleSkillBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_ParentFrameId",
                table: "ChatFrames",
                column: "ParentFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_SessionId_Depth",
                table: "ChatFrames",
                columns: new[] { "SessionId", "Depth" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_TaskId",
                table: "ChatFrames",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_FrameId_SequenceNo",
                table: "ChatMessages",
                columns: new[] { "FrameId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ParentMessageId",
                table: "ChatMessages",
                column: "ParentMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SessionId_SequenceNo",
                table: "ChatMessages",
                columns: new[] { "SessionId", "SequenceNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_CreatedAt",
                table: "ChatSessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ProjectId",
                table: "ChatSessions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ProjectId_CreatedAt",
                table: "ChatSessions",
                columns: new[] { "ProjectId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ProjectId_Scene_CreatedAt",
                table: "ChatSessions",
                columns: new[] { "ProjectId", "Scene", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_Status",
                table: "ChatSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_AgentId",
                table: "checkpoints",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_ProjectId",
                table: "checkpoints",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_checkpoints_TaskId",
                table: "checkpoints",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_global_settings_Key",
                table: "global_settings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSkills_InstallKey",
                table: "InstalledSkills",
                column: "InstallKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSkills_Owner_Repo_SkillId",
                table: "InstalledSkills",
                columns: new[] { "Owner", "Repo", "SkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSkills_ProjectId",
                table: "InstalledSkills",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSkills_Scope_ProjectId",
                table: "InstalledSkills",
                columns: new[] { "Scope", "ProjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_InstalledSkills_SourceKey",
                table: "InstalledSkills",
                column: "SourceKey");

            migrationBuilder.CreateIndex(
                name: "IX_McpServerConfigs_IsEnabled",
                table: "McpServerConfigs",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_McpServerConfigs_McpServerId",
                table: "McpServerConfigs",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_Category",
                table: "McpServers",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_Mode",
                table: "McpServers",
                column: "Mode");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_Name",
                table: "McpServers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_Source",
                table: "McpServers",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_project_agents_AgentRoleId",
                table: "project_agents",
                column: "AgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_project_agents_ProjectId",
                table: "project_agents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentMcpBindings_IsEnabled",
                table: "ProjectAgentMcpBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentMcpBindings_McpServerId",
                table: "ProjectAgentMcpBindings",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentMcpBindings_ProjectAgentId_McpServerId",
                table: "ProjectAgentMcpBindings",
                columns: new[] { "ProjectAgentId", "McpServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentSkillBindings_IsEnabled",
                table: "ProjectAgentSkillBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentSkillBindings_ProjectAgentId_SkillInstallKey",
                table: "ProjectAgentSkillBindings",
                columns: new[] { "ProjectAgentId", "SkillInstallKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CreatedAt",
                table: "projects",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                table: "projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_UpdatedAt",
                table: "projects",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_IsEnabled",
                table: "ProviderAccounts",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_ProtocolType",
                table: "ProviderAccounts",
                column: "ProtocolType");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_FrameId",
                table: "SessionEvents",
                column: "FrameId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_MessageId",
                table: "SessionEvents",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_SessionId_SequenceNo",
                table: "SessionEvents",
                columns: new[] { "SessionId", "SequenceNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_DependsOnId",
                table: "task_dependencies",
                column: "DependsOnId");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_TaskId_DependsOnId",
                table: "task_dependencies",
                columns: new[] { "TaskId", "DependsOnId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Priority",
                table: "tasks",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_ProjectId",
                table: "tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_Status",
                table: "tasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_AssignedAgentId",
                table: "tasks",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ParentTaskId",
                table: "tasks",
                column: "ParentTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_events");

            migrationBuilder.DropTable(
                name: "AgentRoleMcpBindings");

            migrationBuilder.DropTable(
                name: "AgentRoleMcpConfigs");

            migrationBuilder.DropTable(
                name: "AgentRoleSkillBindings");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "global_settings");

            migrationBuilder.DropTable(
                name: "InstalledSkills");

            migrationBuilder.DropTable(
                name: "ProjectAgentMcpBindings");

            migrationBuilder.DropTable(
                name: "ProjectAgentSkillBindings");

            migrationBuilder.DropTable(
                name: "ProviderAccounts");

            migrationBuilder.DropTable(
                name: "SessionEvents");

            migrationBuilder.DropTable(
                name: "task_dependencies");

            migrationBuilder.DropTable(
                name: "McpServerConfigs");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropTable(
                name: "McpServers");

            migrationBuilder.DropTable(
                name: "ChatFrames");

            migrationBuilder.DropTable(
                name: "ChatSessions");

            migrationBuilder.DropTable(
                name: "tasks");

            migrationBuilder.DropTable(
                name: "project_agents");

            migrationBuilder.DropTable(
                name: "agent_roles");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "plugins");
        }
    }
}
