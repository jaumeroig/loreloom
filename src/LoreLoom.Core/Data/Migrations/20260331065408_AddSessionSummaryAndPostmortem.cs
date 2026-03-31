using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreLoom.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionSummaryAndPostmortem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastSummaryTurn",
                table: "Games",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Postmortem",
                table: "Games",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionSummary",
                table: "Games",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSummaryTurn",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "Postmortem",
                table: "Games");

            migrationBuilder.DropColumn(
                name: "SessionSummary",
                table: "Games");
        }
    }
}
