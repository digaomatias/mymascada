using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddAiUsageCheckConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_AiCategorizationUsage_LlmCategorizationCount",
                table: "AiCategorizationUsages",
                sql: "\"LlmCategorizationCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiCategorizationUsage_Month",
                table: "AiCategorizationUsages",
                sql: "\"Month\" BETWEEN 1 AND 12");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AiCategorizationUsage_RuleSuggestionCount",
                table: "AiCategorizationUsages",
                sql: "\"RuleSuggestionCount\" >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AiCategorizationUsage_LlmCategorizationCount",
                table: "AiCategorizationUsages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiCategorizationUsage_Month",
                table: "AiCategorizationUsages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_AiCategorizationUsage_RuleSuggestionCount",
                table: "AiCategorizationUsages");
        }
    }
}
