using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSoulColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Soul",
                table: "agent_roles",
                type: "TEXT",
                nullable: true);

            // 迁移旧数据：从 Config JSON 中提取 soul 到新 Soul 列
            migrationBuilder.Sql("""
                UPDATE agent_roles
                SET "Soul" = json_extract("Config", '$.soul')
                WHERE "Config" IS NOT NULL
                  AND json_extract("Config", '$.soul') IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Soul",
                table: "agent_roles");
        }
    }
}
