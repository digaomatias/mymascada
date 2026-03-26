using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenRevocationTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRevocationPending",
                table: "AkahuUserCredentials",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevocationFailedAt",
                table: "AkahuUserCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RevocationFailureCount",
                table: "AkahuUserCredentials",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsRevocationPending",
                table: "AkahuUserCredentials");

            migrationBuilder.DropColumn(
                name: "RevocationFailedAt",
                table: "AkahuUserCredentials");

            migrationBuilder.DropColumn(
                name: "RevocationFailureCount",
                table: "AkahuUserCredentials");
        }
    }
}
