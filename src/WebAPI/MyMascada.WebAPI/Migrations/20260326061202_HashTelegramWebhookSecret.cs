using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class HashTelegramWebhookSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WebhookSecret",
                table: "UserTelegramSettings",
                newName: "WebhookSecretHash");

            // Hash existing plaintext webhook secrets using SHA-256
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pgcrypto;
                UPDATE "UserTelegramSettings"
                SET "WebhookSecretHash" = encode(digest("WebhookSecretHash", 'sha256'), 'hex')
                WHERE "WebhookSecretHash" IS NOT NULL AND "WebhookSecretHash" != '';
                """);

            migrationBuilder.RenameIndex(
                name: "IX_UserTelegramSettings_WebhookSecret",
                table: "UserTelegramSettings",
                newName: "IX_UserTelegramSettings_WebhookSecretHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "WebhookSecretHash",
                table: "UserTelegramSettings",
                newName: "WebhookSecret");

            migrationBuilder.RenameIndex(
                name: "IX_UserTelegramSettings_WebhookSecretHash",
                table: "UserTelegramSettings",
                newName: "IX_UserTelegramSettings_WebhookSecret");
        }
    }
}
