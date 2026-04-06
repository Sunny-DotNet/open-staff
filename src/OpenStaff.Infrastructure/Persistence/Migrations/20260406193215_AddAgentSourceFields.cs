using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSourceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "agent_roles",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "VendorType",
                table: "agent_roles",
                type: "TEXT",
                nullable: true);

            // 数据迁移：IsBuiltin=true → Source=Builtin(1)
            migrationBuilder.Sql("UPDATE agent_roles SET Source = 1 WHERE IsBuiltin = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                table: "agent_roles");

            migrationBuilder.DropColumn(
                name: "VendorType",
                table: "agent_roles");
        }
    }
}
