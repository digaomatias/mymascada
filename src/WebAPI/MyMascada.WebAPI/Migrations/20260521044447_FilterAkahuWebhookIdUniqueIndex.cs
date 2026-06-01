using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FilterAkahuWebhookIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AkahuWebhookSubscriptions_WebhookId",
                table: "AkahuWebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_AkahuWebhookSubscriptions_WebhookId",
                table: "AkahuWebhookSubscriptions",
                column: "WebhookId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AkahuWebhookSubscriptions_WebhookId",
                table: "AkahuWebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_AkahuWebhookSubscriptions_WebhookId",
                table: "AkahuWebhookSubscriptions",
                column: "WebhookId",
                unique: true);
        }
    }
}
