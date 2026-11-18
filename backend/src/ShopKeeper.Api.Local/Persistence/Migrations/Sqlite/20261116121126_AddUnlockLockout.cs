using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Api.Local.Persistence.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class AddUnlockLockout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FailedUnlockAttempts",
                table: "LocalAppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UnlockLockedUntil",
                table: "LocalAppSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailedUnlockAttempts",
                table: "LocalAppSettings");

            migrationBuilder.DropColumn(
                name: "UnlockLockedUntil",
                table: "LocalAppSettings");
        }
    }
}
