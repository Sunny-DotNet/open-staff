using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OpenStaff.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameEnvConfigEncryptedToEnvConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EnvConfigEncrypted",
                table: "ProviderAccounts",
                newName: "EnvConfig");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "EnvConfig",
                table: "ProviderAccounts",
                newName: "EnvConfigEncrypted");
        }
    }
}
