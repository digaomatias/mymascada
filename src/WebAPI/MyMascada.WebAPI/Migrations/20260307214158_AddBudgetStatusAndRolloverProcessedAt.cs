using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetStatusAndRolloverProcessedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new columns first (before dropping IsActive)
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Budgets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "RolloverProcessedAt",
                table: "Budgets",
                type: "timestamp with time zone",
                nullable: true);

            // Migrate data: IsActive=true → Status=0 (Active), IsActive=false → Status=1 (Completed)
            migrationBuilder.Sql(
                "UPDATE \"Budgets\" SET \"Status\" = CASE WHEN \"IsActive\" = true THEN 0 ELSE 1 END");

            // Now safe to drop old column and index
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_IsActive",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Budgets");

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_Status",
                table: "Budgets",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Budgets_UserId_Status",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "RolloverProcessedAt",
                table: "Budgets");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Budgets");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Budgets",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Budgets_UserId_IsActive",
                table: "Budgets",
                columns: new[] { "UserId", "IsActive" });
        }
    }
}
