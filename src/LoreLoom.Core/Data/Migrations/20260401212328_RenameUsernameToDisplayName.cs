using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreLoom.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameUsernameToDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_Username",
                table: "Accounts");

            migrationBuilder.RenameColumn(
                name: "Username",
                table: "Accounts",
                newName: "DisplayName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "Accounts",
                newName: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);
        }
    }
}
