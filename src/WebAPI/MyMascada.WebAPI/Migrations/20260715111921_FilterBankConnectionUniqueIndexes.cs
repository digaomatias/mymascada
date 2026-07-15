using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FilterBankConnectionUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankConnections_AccountId",
                table: "BankConnections");

            migrationBuilder.DropIndex(
                name: "IX_BankConnections_AccountId_ProviderId",
                table: "BankConnections");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_AccountId",
                table: "BankConnections",
                column: "AccountId",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BankConnections_AccountId",
                table: "BankConnections");

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_AccountId",
                table: "BankConnections",
                column: "AccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankConnections_AccountId_ProviderId",
                table: "BankConnections",
                columns: new[] { "AccountId", "ProviderId" },
                unique: true);
        }
    }
}
