using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopKeeper.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledReportLastRunStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastRunError",
                table: "ScheduledReports",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LastRunSucceeded",
                table: "ScheduledReports",
                type: "boolean",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastRunError",
                table: "ScheduledReports");

            migrationBuilder.DropColumn(
                name: "LastRunSucceeded",
                table: "ScheduledReports");
        }
    }
}
