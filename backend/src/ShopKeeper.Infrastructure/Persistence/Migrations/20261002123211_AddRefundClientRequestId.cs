using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundClientRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "Refunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_BusinessId_ClientRequestId",
                table: "Refunds",
                columns: new[] { "BusinessId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_BusinessId_ClientRequestId",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "Refunds");
        }
    }
}
