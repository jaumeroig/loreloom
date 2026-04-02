using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreLoom.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCreatorToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatorToken",
                table: "Games",
                type: "TEXT",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE "Games"
                SET "CreatorToken" = COALESCE(
                    (
                        SELECT p."Token"
                        FROM "Turns" AS t
                        INNER JOIN "Players" AS p ON p."Id" = t."PlayerId"
                        WHERE t."GameId" = "Games"."Id"
                        ORDER BY t."CreatedAt"
                        LIMIT 1
                    ),
                    (
                        SELECT p."Token"
                        FROM "Players" AS p
                        WHERE p."GameId" = "Games"."Id" AND p."IsCurrentTurn" = 1
                        LIMIT 1
                    ),
                    (
                        SELECT p."Token"
                        FROM "Players" AS p
                        WHERE p."GameId" = "Games"."Id"
                        ORDER BY p.rowid
                        LIMIT 1
                    ),
                    ""
                )
                WHERE "CreatorToken" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatorToken",
                table: "Games");
        }
    }
}
