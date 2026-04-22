using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionPackages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionPackageId",
                table: "SessionEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceEffectIndex",
                table: "SessionEvents",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceFrameId",
                table: "SessionEvents",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionPackageId",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "OriginatingFrameId",
                table: "ChatMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExecutionPackageId",
                table: "ChatFrames",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExecutionPackages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ParentExecutionPackageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RetryOfExecutionPackageId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SourceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RootFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EntryKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PackageKind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Scene = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    InputSummary = table.Column<string>(type: "TEXT", nullable: true),
                    InitiatorRole = table.Column<string>(type: "TEXT", nullable: true),
                    TargetRole = table.Column<string>(type: "TEXT", nullable: true),
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjectAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EffectsJson = table.Column<string>(type: "TEXT", nullable: true),
                    SnapshotPath = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExecutionPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_ChatFrames_RootFrameId",
                        column: x => x.RootFrameId,
                        principalTable: "ChatFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_ChatFrames_SourceFrameId",
                        column: x => x.SourceFrameId,
                        principalTable: "ChatFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_ChatSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "ChatSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_ExecutionPackages_ParentExecutionPackageId",
                        column: x => x.ParentExecutionPackageId,
                        principalTable: "ExecutionPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_ExecutionPackages_RetryOfExecutionPackageId",
                        column: x => x.RetryOfExecutionPackageId,
                        principalTable: "ExecutionPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExecutionPackages_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TaskExecutionLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ExecutionPackageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    SourceEffectIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskExecutionLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskExecutionLinks_ExecutionPackages_ExecutionPackageId",
                        column: x => x.ExecutionPackageId,
                        principalTable: "ExecutionPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TaskExecutionLinks_tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionEvents_ExecutionPackageId",
                table: "SessionEvents",
                column: "ExecutionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ExecutionPackageId",
                table: "ChatMessages",
                column: "ExecutionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_OriginatingFrameId",
                table: "ChatMessages",
                column: "OriginatingFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatFrames_ExecutionPackageId",
                table: "ChatFrames",
                column: "ExecutionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_ParentExecutionPackageId",
                table: "ExecutionPackages",
                column: "ParentExecutionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_ProjectId_EntryKind_CreatedAt",
                table: "ExecutionPackages",
                columns: new[] { "ProjectId", "EntryKind", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_RetryOfExecutionPackageId",
                table: "ExecutionPackages",
                column: "RetryOfExecutionPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_RootFrameId",
                table: "ExecutionPackages",
                column: "RootFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_SessionId_CreatedAt",
                table: "ExecutionPackages",
                columns: new[] { "SessionId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_SessionId_Status_CreatedAt",
                table: "ExecutionPackages",
                columns: new[] { "SessionId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_SourceFrameId",
                table: "ExecutionPackages",
                column: "SourceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_ExecutionPackages_TaskId",
                table: "ExecutionPackages",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionLinks_ExecutionPackageId_SourceEffectIndex",
                table: "TaskExecutionLinks",
                columns: new[] { "ExecutionPackageId", "SourceEffectIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskExecutionLinks_TaskId_CreatedAt",
                table: "TaskExecutionLinks",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_ChatFrames_ExecutionPackages_ExecutionPackageId",
                table: "ChatFrames",
                column: "ExecutionPackageId",
                principalTable: "ExecutionPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatFrames_OriginatingFrameId",
                table: "ChatMessages",
                column: "OriginatingFrameId",
                principalTable: "ChatFrames",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ExecutionPackages_ExecutionPackageId",
                table: "ChatMessages",
                column: "ExecutionPackageId",
                principalTable: "ExecutionPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SessionEvents_ExecutionPackages_ExecutionPackageId",
                table: "SessionEvents",
                column: "ExecutionPackageId",
                principalTable: "ExecutionPackages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatFrames_ExecutionPackages_ExecutionPackageId",
                table: "ChatFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatFrames_OriginatingFrameId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ExecutionPackages_ExecutionPackageId",
                table: "ChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_SessionEvents_ExecutionPackages_ExecutionPackageId",
                table: "SessionEvents");

            migrationBuilder.DropTable(
                name: "TaskExecutionLinks");

            migrationBuilder.DropTable(
                name: "ExecutionPackages");

            migrationBuilder.DropIndex(
                name: "IX_SessionEvents_ExecutionPackageId",
                table: "SessionEvents");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ExecutionPackageId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_OriginatingFrameId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatFrames_ExecutionPackageId",
                table: "ChatFrames");

            migrationBuilder.DropColumn(
                name: "ExecutionPackageId",
                table: "SessionEvents");

            migrationBuilder.DropColumn(
                name: "SourceEffectIndex",
                table: "SessionEvents");

            migrationBuilder.DropColumn(
                name: "SourceFrameId",
                table: "SessionEvents");

            migrationBuilder.DropColumn(
                name: "ExecutionPackageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "OriginatingFrameId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ExecutionPackageId",
                table: "ChatFrames");
        }
    }
}
