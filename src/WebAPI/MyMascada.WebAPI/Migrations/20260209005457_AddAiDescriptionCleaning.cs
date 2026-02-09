using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAiDescriptionCleaning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiDescriptionCleaning",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDescriptionCleaning",
                table: "Users");
        }
    }
}
