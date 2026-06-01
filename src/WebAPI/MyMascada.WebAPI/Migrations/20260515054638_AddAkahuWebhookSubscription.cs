using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAkahuWebhookSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AkahuWebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AkahuUserCredentialId = table.Column<int>(type: "integer", nullable: false),
                    WebhookId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    WebhookType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SubscribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastReconcileError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AkahuWebhookSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AkahuWebhookSubscriptions_AkahuUserCredentials_AkahuUserCre~",
                        column: x => x.AkahuUserCredentialId,
                        principalTable: "AkahuUserCredentials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AkahuWebhookSubscriptions_AkahuUserCredentialId",
                table: "AkahuWebhookSubscriptions",
                column: "AkahuUserCredentialId");

            migrationBuilder.CreateIndex(
                name: "IX_AkahuWebhookSubscriptions_UserId_WebhookType",
                table: "AkahuWebhookSubscriptions",
                columns: new[] { "UserId", "WebhookType" },
                unique: true,
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "IX_AkahuWebhookSubscriptions_WebhookId",
                table: "AkahuWebhookSubscriptions",
                column: "WebhookId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AkahuWebhookSubscriptions");
        }
    }
}
