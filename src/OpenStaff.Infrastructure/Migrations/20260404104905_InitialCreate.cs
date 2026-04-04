using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Migrations
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
                    TechStack = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false, defaultValue: "zh-CN"),
                    WorkspacePath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    GitConfig = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "initializing"),
                    Metadata = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "agent_roles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    RoleType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    SystemPrompt = table.Column<string>(type: "TEXT", nullable: true),
                    ModelProviderId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ModelName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsBuiltin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    PluginId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Config = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
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
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_dependencies", x => new { x.TaskId, x.DependsOnId });
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
                name: "IX_agent_roles_PluginId",
                table: "agent_roles",
                column: "PluginId");

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
                name: "IX_project_agents_AgentRoleId",
                table: "project_agents",
                column: "AgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_project_agents_ProjectId",
                table: "project_agents",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_task_dependencies_DependsOnId",
                table: "task_dependencies",
                column: "DependsOnId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_AssignedAgentId",
                table: "tasks",
                column: "AssignedAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ParentTaskId",
                table: "tasks",
                column: "ParentTaskId");

            migrationBuilder.CreateIndex(
                name: "IX_tasks_ProjectId",
                table: "tasks",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_events");

            migrationBuilder.DropTable(
                name: "checkpoints");

            migrationBuilder.DropTable(
                name: "global_settings");

            migrationBuilder.DropTable(
                name: "task_dependencies");

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
