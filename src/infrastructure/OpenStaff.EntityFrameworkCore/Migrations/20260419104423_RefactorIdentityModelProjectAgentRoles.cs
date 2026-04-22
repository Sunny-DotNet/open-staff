using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Migrations
{
    /// <inheritdoc />
    public partial class RefactorIdentityModelProjectAgentRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_events_project_agents_AgentId",
                table: "agent_events");

            migrationBuilder.DropForeignKey(
                name: "FK_checkpoints_project_agents_AgentId",
                table: "checkpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_project_agents_AssignedAgentId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "ProjectAgentMcpBindings");

            migrationBuilder.DropTable(
                name: "ProjectAgentSkillBindings");

            migrationBuilder.DropTable(
                name: "project_agents");

            migrationBuilder.DropColumn(
                name: "InitiatorRole",
                table: "ChatFrames");

            migrationBuilder.RenameColumn(
                name: "AssignedAgentId",
                table: "tasks",
                newName: "AssignedProjectAgentRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_tasks_AssignedAgentId",
                table: "tasks",
                newName: "IX_tasks_AssignedProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "TargetRole",
                table: "ExecutionPackages",
                newName: "TargetProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "ProjectAgentId",
                table: "ExecutionPackages",
                newName: "TargetAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "InitiatorRole",
                table: "ExecutionPackages",
                newName: "ProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "checkpoints",
                newName: "ProjectAgentRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_checkpoints_AgentId",
                table: "checkpoints",
                newName: "IX_checkpoints_ProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "AgentRole",
                table: "ChatMessages",
                newName: "ProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "TargetRole",
                table: "ChatFrames",
                newName: "TargetProjectAgentRoleId");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "agent_events",
                newName: "ProjectAgentRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_AgentEvents_ProjectId_AgentId",
                table: "agent_events",
                newName: "IX_AgentEvents_ProjectId_ProjectAgentRoleId");

            migrationBuilder.RenameIndex(
                name: "IX_agent_events_AgentId_CreatedAt",
                table: "agent_events",
                newName: "IX_agent_events_ProjectAgentRoleId_CreatedAt");

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorAgentRoleId",
                table: "ExecutionPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorProjectAgentRoleId",
                table: "ExecutionPackages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgentRoleId",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorAgentRoleId",
                table: "ChatFrames",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InitiatorProjectAgentRoleId",
                table: "ChatFrames",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetAgentRoleId",
                table: "ChatFrames",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "project_agent_roles",
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
                    table.PrimaryKey("PK_project_agent_roles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_project_agent_roles_agent_roles_AgentRoleId",
                        column: x => x.AgentRoleId,
                        principalTable: "agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_project_agent_roles_projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentRoleMcpBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectAgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolFilter = table.Column<string>(type: "text", nullable: true),
                    RuntimeParameters = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectAgentRoleMcpBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentRoleMcpBindings_McpServers_McpServerId",
                        column: x => x.McpServerId,
                        principalTable: "McpServers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectAgentRoleMcpBindings_project_agent_roles_ProjectAgentRoleId",
                        column: x => x.ProjectAgentRoleId,
                        principalTable: "project_agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectAgentRoleSkillBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectAgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
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
                    table.PrimaryKey("PK_ProjectAgentRoleSkillBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectAgentRoleSkillBindings_project_agent_roles_ProjectAgentRoleId",
                        column: x => x.ProjectAgentRoleId,
                        principalTable: "project_agent_roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_AgentRoleId",
                table: "ExecutionPackages",
                column: "AgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_InitiatorAgentRoleId",
                table: "ExecutionPackages",
                column: "InitiatorAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_InitiatorProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "InitiatorProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_ProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "ProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_TargetAgentRoleId",
                table: "ExecutionPackages",
                column: "TargetAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_TargetProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "TargetProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_AgentRoleId",
                table: "ChatMessages",
                column: "AgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ProjectAgentRoleId",
                table: "ChatMessages",
                column: "ProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_InitiatorAgentRoleId",
                table: "ChatFrames",
                column: "InitiatorAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_InitiatorProjectAgentRoleId",
                table: "ChatFrames",
                column: "InitiatorProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_TargetAgentRoleId",
                table: "ChatFrames",
                column: "TargetAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_TargetProjectAgentRoleId",
                table: "ChatFrames",
                column: "TargetProjectAgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_project_agent_roles_AgentRoleId",
                table: "project_agent_roles",
                column: "AgentRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_project_agent_roles_ProjectId_AgentRoleId",
                table: "project_agent_roles",
                columns: new[] { "ProjectId", "AgentRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentRoleMcpBindings_IsEnabled",
                table: "ProjectAgentRoleMcpBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentRoleMcpBindings_McpServerId",
                table: "ProjectAgentRoleMcpBindings",
                column: "McpServerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentRoleMcpBindings_ProjectAgentRoleId_McpServerId",
                table: "ProjectAgentRoleMcpBindings",
                columns: new[] { "ProjectAgentRoleId", "McpServerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentRoleSkillBindings_IsEnabled",
                table: "ProjectAgentRoleSkillBindings",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectAgentRoleSkillBindings_ProjectAgentRoleId_SkillInstallKey",
                table: "ProjectAgentRoleSkillBindings",
                columns: new[] { "ProjectAgentRoleId", "SkillInstallKey" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_agent_events_project_agent_roles_ProjectAgentRoleId",
                table: "agent_events",
                column: "ProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatFrames_agent_roles_InitiatorAgentRoleId",
                table: "ChatFrames",
                column: "InitiatorAgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatFrames_agent_roles_TargetAgentRoleId",
                table: "ChatFrames",
                column: "TargetAgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatFrames_project_agent_roles_InitiatorProjectAgentRoleId",
                table: "ChatFrames",
                column: "InitiatorProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatFrames_project_agent_roles_TargetProjectAgentRoleId",
                table: "ChatFrames",
                column: "TargetProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_agent_roles_AgentRoleId",
                table: "ChatMessages",
                column: "AgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_project_agent_roles_ProjectAgentRoleId",
                table: "ChatMessages",
                column: "ProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_checkpoints_project_agent_roles_ProjectAgentRoleId",
                table: "checkpoints",
                column: "ProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_agent_roles_AgentRoleId",
                table: "ExecutionPackages",
                column: "AgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_agent_roles_InitiatorAgentRoleId",
                table: "ExecutionPackages",
                column: "InitiatorAgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_agent_roles_TargetAgentRoleId",
                table: "ExecutionPackages",
                column: "TargetAgentRoleId",
                principalTable: "agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_InitiatorProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "InitiatorProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_ProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "ProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_TargetProjectAgentRoleId",
                table: "ExecutionPackages",
                column: "TargetProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_project_agent_roles_AssignedProjectAgentRoleId",
                table: "tasks",
                column: "AssignedProjectAgentRoleId",
                principalTable: "project_agent_roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_events_project_agent_roles_ProjectAgentRoleId",
                table: "agent_events");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatFrames_agent_roles_InitiatorAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatFrames_agent_roles_TargetAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatFrames_project_agent_roles_InitiatorProjectAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatFrames_project_agent_roles_TargetProjectAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_agent_roles_AgentRoleId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_project_agent_roles_ProjectAgentRoleId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_checkpoints_project_agent_roles_ProjectAgentRoleId",
                table: "checkpoints");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_agent_roles_AgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_agent_roles_InitiatorAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_agent_roles_TargetAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_InitiatorProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_ProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ExecutionPackages_project_agent_roles_TargetProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_tasks_project_agent_roles_AssignedProjectAgentRoleId",
                table: "tasks");

            migrationBuilder.DropTable(
                name: "ProjectAgentRoleMcpBindings");

            migrationBuilder.DropTable(
                name: "ProjectAgentRoleSkillBindings");

            migrationBuilder.DropTable(
                name: "project_agent_roles");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_AgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_InitiatorAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_InitiatorProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_ProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_TargetAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ExecutionPackages_TargetProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_AgentRoleId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ProjectAgentRoleId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatFrames_InitiatorAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropIndex(
                name: "IX_ChatFrames_InitiatorProjectAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropIndex(
                name: "IX_ChatFrames_TargetAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropIndex(
                name: "IX_ChatFrames_TargetProjectAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropColumn(
                name: "InitiatorAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropColumn(
                name: "InitiatorProjectAgentRoleId",
                table: "ExecutionPackages");

            migrationBuilder.DropColumn(
                name: "AgentRoleId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "InitiatorAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropColumn(
                name: "InitiatorProjectAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.DropColumn(
                name: "TargetAgentRoleId",
                table: "ChatFrames");

            migrationBuilder.RenameColumn(
                name: "AssignedProjectAgentRoleId",
                table: "tasks",
                newName: "AssignedAgentId");

            migrationBuilder.RenameIndex(
                name: "IX_tasks_AssignedProjectAgentRoleId",
                table: "tasks",
                newName: "IX_tasks_AssignedAgentId");

            migrationBuilder.RenameColumn(
                name: "TargetProjectAgentRoleId",
                table: "ExecutionPackages",
                newName: "TargetRole");

            migrationBuilder.RenameColumn(
                name: "TargetAgentRoleId",
                table: "ExecutionPackages",
                newName: "ProjectAgentId");

            migrationBuilder.RenameColumn(
                name: "ProjectAgentRoleId",
                table: "ExecutionPackages",
                newName: "InitiatorRole");

            migrationBuilder.RenameColumn(
                name: "ProjectAgentRoleId",
                table: "checkpoints",
                newName: "AgentId");

            migrationBuilder.RenameIndex(
                name: "IX_checkpoints_ProjectAgentRoleId",
                table: "checkpoints",
                newName: "IX_checkpoints_AgentId");

            migrationBuilder.RenameColumn(
                name: "ProjectAgentRoleId",
                table: "ChatMessages",
                newName: "AgentRole");

            migrationBuilder.RenameColumn(
                name: "TargetProjectAgentRoleId",
                table: "ChatFrames",
                newName: "TargetRole");

            migrationBuilder.RenameColumn(
                name: "ProjectAgentRoleId",
                table: "agent_events",
                newName: "AgentId");

            migrationBuilder.RenameIndex(
                name: "IX_AgentEvents_ProjectId_ProjectAgentRoleId",
                table: "agent_events",
                newName: "IX_AgentEvents_ProjectId_AgentId");

            migrationBuilder.RenameIndex(
                name: "IX_agent_events_ProjectAgentRoleId_CreatedAt",
                table: "agent_events",
                newName: "IX_agent_events_AgentId_CreatedAt");

            migrationBuilder.AddColumn<string>(
                name: "InitiatorRole",
                table: "ChatFrames",
                type: "TEXT",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "project_agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CurrentTask = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "idle"),
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
                name: "ProjectAgentMcpBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectAgentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    RuntimeParameters = table.Column<string>(type: "text", nullable: true),
                    ToolFilter = table.Column<string>(type: "text", nullable: true),
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
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    GithubUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    Repo = table.Column<string>(type: "TEXT", maxLength: 150, nullable: false),
                    SkillId = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    SkillInstallKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
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

            migrationBuilder.AddForeignKey(
                name: "FK_agent_events_project_agents_AgentId",
                table: "agent_events",
                column: "AgentId",
                principalTable: "project_agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_checkpoints_project_agents_AgentId",
                table: "checkpoints",
                column: "AgentId",
                principalTable: "project_agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_tasks_project_agents_AssignedAgentId",
                table: "tasks",
                column: "AssignedAgentId",
                principalTable: "project_agents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
