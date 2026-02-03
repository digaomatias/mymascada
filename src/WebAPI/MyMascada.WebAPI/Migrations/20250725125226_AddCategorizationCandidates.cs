using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCategorizationCandidates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategorizationCandidates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TransactionId = table.Column<int>(type: "integer", nullable: false),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    CategorizationMethod = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    ProcessedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Reasoning = table.Column<string>(type: "TEXT", nullable: true),
                    Metadata = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AppliedBy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategorizationCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategorizationCandidates_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CategorizationCandidates_Transactions_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationCandidates_CategorizationMethod",
                table: "CategorizationCandidates",
                column: "CategorizationMethod");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationCandidates_CategoryId",
                table: "CategorizationCandidates",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationCandidates_Status",
                table: "CategorizationCandidates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationCandidates_TransactionId",
                table: "CategorizationCandidates",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_CategorizationCandidates_TransactionId_Status",
                table: "CategorizationCandidates",
                columns: new[] { "TransactionId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategorizationCandidates");
        }
    }
}
