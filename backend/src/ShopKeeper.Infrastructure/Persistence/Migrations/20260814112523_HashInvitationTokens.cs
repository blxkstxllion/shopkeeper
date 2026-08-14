using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HashInvitationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Token",
                table: "PendingInvitations",
                newName: "TokenHash");

            migrationBuilder.RenameIndex(
                name: "IX_PendingInvitations_Token",
                table: "PendingInvitations",
                newName: "IX_PendingInvitations_TokenHash");

            // Any row from before this migration still holds its old raw token value in what is
            // now the TokenHash column - already unusable for lookup (nothing will ever hash to
            // it), but explicitly expiring not-yet-accepted invites here means the old, no-longer-
            // functioning links fail with a clear "expired" message instead of a silent 404, and
            // nothing is left relying on a plaintext value sitting in the database.
            migrationBuilder.Sql(
                """
                UPDATE "PendingInvitations"
                SET "ExpiresAt" = NOW()
                WHERE "AcceptedAt" IS NULL AND "ExpiresAt" > NOW();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TokenHash",
                table: "PendingInvitations",
                newName: "Token");

            migrationBuilder.RenameIndex(
                name: "IX_PendingInvitations_TokenHash",
                table: "PendingInvitations",
                newName: "IX_PendingInvitations_Token");
        }
    }
}
