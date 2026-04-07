using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameVendorTypeToProviderType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "VendorType",
                table: "agent_roles",
                newName: "ProviderType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ProviderType",
                table: "agent_roles",
                newName: "VendorType");
        }
    }
}
