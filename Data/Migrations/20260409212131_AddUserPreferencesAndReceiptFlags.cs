using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPreferencesAndReceiptFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_UserId",
                table: "Receipts");

            migrationBuilder.AddColumn<bool>(
                name: "AnomalyNotificationsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "BudgetNotificationsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "MonthlyReportEmailEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SubscriptionNotificationsEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "WeeklySummaryDay",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monday");

            migrationBuilder.AddColumn<bool>(
                name: "WeeklySummaryEmailEnabled",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsMarkedDuplicate",
                table: "Receipts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId_UploadedAt",
                table: "Receipts",
                columns: new[] { "UserId", "UploadedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Receipts_UserId_UploadedAt",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "AnomalyNotificationsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BudgetNotificationsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MonthlyReportEmailEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SubscriptionNotificationsEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeeklySummaryDay",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "WeeklySummaryEmailEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsMarkedDuplicate",
                table: "Receipts");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_UserId",
                table: "Receipts",
                column: "UserId");
        }
    }
}
