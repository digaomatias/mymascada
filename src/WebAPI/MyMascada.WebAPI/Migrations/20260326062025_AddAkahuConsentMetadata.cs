using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAkahuConsentMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConsentCorrelationId",
                table: "AkahuUserCredentials",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentGrantedAt",
                table: "AkahuUserCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ConsentRevokedAt",
                table: "AkahuUserCredentials",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentScope",
                table: "AkahuUserCredentials",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConsentCorrelationId",
                table: "AkahuUserCredentials");

            migrationBuilder.DropColumn(
                name: "ConsentGrantedAt",
                table: "AkahuUserCredentials");

            migrationBuilder.DropColumn(
                name: "ConsentRevokedAt",
                table: "AkahuUserCredentials");

            migrationBuilder.DropColumn(
                name: "ConsentScope",
                table: "AkahuUserCredentials");
        }
    }
}
