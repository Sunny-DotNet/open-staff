using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProjectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "TechStack", table: "projects");
            migrationBuilder.DropColumn(name: "GitConfig", table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "DefaultProviderId",
                table: "projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultModelName",
                table: "projects",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtraConfig",
                table: "projects",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DefaultProviderId", table: "projects");
            migrationBuilder.DropColumn(name: "DefaultModelName", table: "projects");
            migrationBuilder.DropColumn(name: "ExtraConfig", table: "projects");

            migrationBuilder.AddColumn<string>(
                name: "TechStack",
                table: "projects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GitConfig",
                table: "projects",
                type: "TEXT",
                nullable: true);
        }
    }
}
