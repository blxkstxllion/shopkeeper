using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingInvitationPartialUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingInvitations_BusinessId_Email",
                table: "PendingInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvitations_BusinessId_Email",
                table: "PendingInvitations",
                columns: new[] { "BusinessId", "Email" },
                unique: true,
                filter: "\"AcceptedAt\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingInvitations_BusinessId_Email",
                table: "PendingInvitations");

            migrationBuilder.CreateIndex(
                name: "IX_PendingInvitations_BusinessId_Email",
                table: "PendingInvitations",
                columns: new[] { "BusinessId", "Email" });
        }
    }
}
