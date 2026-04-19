using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Reading_Writing_Platform.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IX_Novels_AuthorUserId
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Novels_AuthorUserId' AND object_id = OBJECT_ID('Novels'))
BEGIN
    CREATE INDEX IX_Novels_AuthorUserId ON Novels (AuthorUserId);
END");

            // IX_Novels_Status_SubmittedForReviewAt
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Novels_Status_SubmittedForReviewAt' AND object_id = OBJECT_ID('Novels'))
BEGIN
    CREATE INDEX IX_Novels_Status_SubmittedForReviewAt ON Novels (Status, SubmittedForReviewAt);
END");

            // IX_Chapters_NovelId_Status_PublishedAt
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Chapters_NovelId_Status_PublishedAt' AND object_id = OBJECT_ID('Chapters'))
BEGIN
    CREATE INDEX IX_Chapters_NovelId_Status_PublishedAt ON Chapters (NovelId, Status, PublishedAt);
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes safely
            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Chapters_NovelId_Status_PublishedAt' AND object_id = OBJECT_ID('Chapters'))
BEGIN
    DROP INDEX IX_Chapters_NovelId_Status_PublishedAt ON Chapters;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Novels_Status_SubmittedForReviewAt' AND object_id = OBJECT_ID('Novels'))
BEGIN
    DROP INDEX IX_Novels_Status_SubmittedForReviewAt ON Novels;
END");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Novels_AuthorUserId' AND object_id = OBJECT_ID('Novels'))
BEGIN
    DROP INDEX IX_Novels_AuthorUserId ON Novels;
END");
        }
    }
}
