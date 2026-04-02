using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LoreLoom.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationAndPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Accounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EmailVerificationToken",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailVerificationTokenExpiry",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Accounts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordResetTokenExpiry",
                table: "Accounts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "EmailVerificationToken",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "EmailVerificationTokenExpiry",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "PasswordResetTokenExpiry",
                table: "Accounts");
        }
    }
}
