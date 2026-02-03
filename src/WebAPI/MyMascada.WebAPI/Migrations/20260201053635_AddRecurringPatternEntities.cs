using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddRecurringPatternEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RecurringPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    MerchantName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NormalizedMerchantKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IntervalDays = table.Column<int>(type: "integer", nullable: false),
                    AverageAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    NextExpectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastObservedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConsecutiveMisses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CategoryId = table.Column<int>(type: "integer", nullable: true),
                    OccurrenceCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringPatterns_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecurringOccurrences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatternId = table.Column<int>(type: "integer", nullable: false),
                    TransactionId = table.Column<int>(type: "integer", nullable: true),
                    ExpectedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActualDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringOccurrences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecurringOccurrences_RecurringPatterns_PatternId",
                        column: x => x.PatternId,
                        principalTable: "RecurringPatterns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RecurringOccurrences_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_PatternId",
                table: "RecurringOccurrences",
                column: "PatternId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_PatternId_ExpectedDate",
                table: "RecurringOccurrences",
                columns: new[] { "PatternId", "ExpectedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_PatternId_Outcome",
                table: "RecurringOccurrences",
                columns: new[] { "PatternId", "Outcome" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringOccurrences_TransactionId",
                table: "RecurringOccurrences",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPatterns_CategoryId",
                table: "RecurringPatterns",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPatterns_UserId",
                table: "RecurringPatterns",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPatterns_UserId_NextExpectedDate",
                table: "RecurringPatterns",
                columns: new[] { "UserId", "NextExpectedDate" });

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPatterns_UserId_NormalizedMerchantKey",
                table: "RecurringPatterns",
                columns: new[] { "UserId", "NormalizedMerchantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringPatterns_UserId_Status",
                table: "RecurringPatterns",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecurringOccurrences");

            migrationBuilder.DropTable(
                name: "RecurringPatterns");
        }
    }
}
