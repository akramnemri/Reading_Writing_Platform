using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reading_Writing_Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewHistoryAndReviewerTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "Novels",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "Novels",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReviewHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NovelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PerformedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReviewHistories_AspNetUsers_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReviewHistories_Novels_NovelId",
                        column: x => x.NovelId,
                        principalTable: "Novels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Novels_ReviewedByUserId",
                table: "Novels",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_NovelId",
                table: "ReviewHistories",
                column: "NovelId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_PerformedAt",
                table: "ReviewHistories",
                column: "PerformedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewHistories_PerformedByUserId",
                table: "ReviewHistories",
                column: "PerformedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Novels_AspNetUsers_ReviewedByUserId",
                table: "Novels",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Novels_AspNetUsers_ReviewedByUserId",
                table: "Novels");

            migrationBuilder.DropTable(
                name: "ReviewHistories");

            migrationBuilder.DropIndex(
                name: "IX_Novels_ReviewedByUserId",
                table: "Novels");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "Novels");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Novels");
        }
    }
}
