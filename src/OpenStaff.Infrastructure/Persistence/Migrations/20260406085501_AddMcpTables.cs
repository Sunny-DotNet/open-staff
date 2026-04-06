using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMcpTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    Source = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DefaultConfig = table.Column<string>(type: "text", nullable: true),
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
                name: "AgentRoleMcpConfigs",
                columns: table => new
                {
                    AgentRoleId = table.Column<Guid>(type: "TEXT", nullable: false),
                    McpServerConfigId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToolFilter = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentRoleMcpConfigs", x => new { x.AgentRoleId, x.McpServerConfigId });
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

            migrationBuilder.CreateIndex(
                name: "IX_AgentRoleMcpConfigs_McpServerConfigId",
                table: "AgentRoleMcpConfigs",
                column: "McpServerConfigId");

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
                name: "IX_McpServers_Name",
                table: "McpServers",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_McpServers_Source",
                table: "McpServers",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentRoleMcpConfigs");

            migrationBuilder.DropTable(
                name: "McpServerConfigs");

            migrationBuilder.DropTable(
                name: "McpServers");
        }
    }
}
