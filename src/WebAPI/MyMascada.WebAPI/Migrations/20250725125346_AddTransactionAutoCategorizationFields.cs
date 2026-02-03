using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionAutoCategorizationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AutoCategorizationConfidence",
                table: "Transactions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AutoCategorizationMethod",
                table: "Transactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoCategorizedAt",
                table: "Transactions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoCategorized",
                table: "Transactions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AutoCategorizationMethod",
                table: "Transactions",
                column: "AutoCategorizationMethod");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IsAutoCategorized",
                table: "Transactions",
                column: "IsAutoCategorized");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_AutoCategorizationMethod",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_IsAutoCategorized",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoCategorizationConfidence",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoCategorizationMethod",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "AutoCategorizedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsAutoCategorized",
                table: "Transactions");
        }
    }
}
