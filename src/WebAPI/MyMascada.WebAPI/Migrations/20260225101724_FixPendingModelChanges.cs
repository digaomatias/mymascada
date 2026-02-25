using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Fixes snapshot drift for DashboardNudgeDismissals.NudgeType.
    /// The original migration (AddDashboardNudgeDismissals) already created the column as
    /// varchar(50) with a unique index, but the snapshot was regenerated without those
    /// constraints during AddGoalIsPinned. This migration re-syncs the snapshot.
    /// The SQL is idempotent — safe to run against databases where the constraints already exist.
    /// </summary>
    public partial class FixPendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Column is already varchar(50) from the original migration, but the snapshot
            // recorded it as "text". ALTER COLUMN to varchar(50) is a no-op if already varchar(50).
            migrationBuilder.AlterColumn<string>(
                name: "NudgeType",
                table: "DashboardNudgeDismissals",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            // Index already exists from the original migration. Use raw SQL with IF NOT EXISTS
            // to avoid failure on databases that already have it.
            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_DashboardNudgeDismissals_UserId_NudgeType"
                ON "DashboardNudgeDismissals" ("UserId", "NudgeType");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DashboardNudgeDismissals_UserId_NudgeType",
                table: "DashboardNudgeDismissals");

            migrationBuilder.AlterColumn<string>(
                name: "NudgeType",
                table: "DashboardNudgeDismissals",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);
        }
    }
}
