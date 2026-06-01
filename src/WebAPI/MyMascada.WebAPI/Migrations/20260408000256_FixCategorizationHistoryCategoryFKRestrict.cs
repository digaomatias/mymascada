using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class FixCategorizationHistoryCategoryFKRestrict : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_CategorizationHistories_Categories_CategoryId",
                table: "CategorizationHistories",
                column: "CategoryId",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
