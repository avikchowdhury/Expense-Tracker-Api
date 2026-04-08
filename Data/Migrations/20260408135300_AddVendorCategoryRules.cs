using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorCategoryRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Receipts_ReceiptId",
                table: "Expenses");

            migrationBuilder.CreateTable(
                name: "VendorCategoryRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    VendorPattern = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorCategoryRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VendorCategoryRules_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VendorCategoryRules_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VendorCategoryRules_CategoryId",
                table: "VendorCategoryRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_VendorCategoryRules_UserId_VendorPattern",
                table: "VendorCategoryRules",
                columns: new[] { "UserId", "VendorPattern" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Receipts_ReceiptId",
                table: "Expenses",
                column: "ReceiptId",
                principalTable: "Receipts",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Expenses_Receipts_ReceiptId",
                table: "Expenses");

            migrationBuilder.DropTable(
                name: "VendorCategoryRules");

            migrationBuilder.AddForeignKey(
                name: "FK_Expenses_Receipts_ReceiptId",
                table: "Expenses",
                column: "ReceiptId",
                principalTable: "Receipts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
