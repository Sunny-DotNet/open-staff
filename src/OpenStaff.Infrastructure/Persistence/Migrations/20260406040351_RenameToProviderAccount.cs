using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameToProviderAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelProviders");

            migrationBuilder.CreateTable(
                name: "ProviderAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProtocolType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    EnvConfigEncrypted = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_IsEnabled",
                table: "ProviderAccounts",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderAccounts_ProtocolType",
                table: "ProviderAccounts",
                column: "ProtocolType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderAccounts");

            migrationBuilder.CreateTable(
                name: "ModelProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApiKeyEncrypted = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ApiKeyEnvVar = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ApiKeyMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExtraConfig = table.Column<string>(type: "text", nullable: true),
                    IsBuiltin = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ModelProviders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviders_IsEnabled",
                table: "ModelProviders",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_ModelProviders_ProviderType",
                table: "ModelProviders",
                column: "ProviderType");
        }
    }
}
