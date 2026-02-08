using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyMascada.WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WaitlistEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Locale = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaitlistEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InvitationCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    NormalizedCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    WaitlistEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    MaxUses = table.Column<int>(type: "integer", nullable: false),
                    UseCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    UpdatedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvitationCodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvitationCodes_Users_ClaimedByUserId",
                        column: x => x.ClaimedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InvitationCodes_WaitlistEntries_WaitlistEntryId",
                        column: x => x.WaitlistEntryId,
                        principalTable: "WaitlistEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitationCodes_ClaimedByUserId",
                table: "InvitationCodes",
                column: "ClaimedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationCodes_ExpiresAt",
                table: "InvitationCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationCodes_NormalizedCode",
                table: "InvitationCodes",
                column: "NormalizedCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvitationCodes_Status",
                table: "InvitationCodes",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationCodes_WaitlistEntryId",
                table: "InvitationCodes",
                column: "WaitlistEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_NormalizedEmail",
                table: "WaitlistEntries",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WaitlistEntries_Status",
                table: "WaitlistEntries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvitationCodes");

            migrationBuilder.DropTable(
                name: "WaitlistEntries");
        }
    }
}
