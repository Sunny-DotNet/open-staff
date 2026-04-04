using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddModelProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ModelProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProviderType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ApiKeyEncrypted = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ApiKeyMode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    ApiKeyEnvVar = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DefaultModel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExtraConfig = table.Column<string>(type: "text", nullable: true),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsBuiltin = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ModelProviders");
        }
    }
}
