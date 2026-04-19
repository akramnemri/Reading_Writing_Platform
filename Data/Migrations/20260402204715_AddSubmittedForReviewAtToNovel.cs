using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reading_Writing_Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmittedForReviewAtToNovel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmittedForReviewAt",
                table: "Novels",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmittedForReviewAt",
                table: "Novels");
        }
    }
}
