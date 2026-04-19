using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.Areas.Admin.Models
{
    public class ReviewDetailsViewModel
    {
        public Novel Novel { get; set; } = null!;
        public List<ChapterListItemViewModel> Chapters { get; set; } = new();
        public string RejectionReason { get; set; } = string.Empty;
        public string AuthorUserId { get; set; } = string.Empty;
        public string? AuthorBlockedUntil { get; set; }
        public int SubmissionCount { get; set; }

        // Reviewer info
        public string? ReviewedByUserName { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
    }

    public class ChapterListItemViewModel
    {
        public Guid Id { get; set; }
        public int ChapterNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public ChapterStatus Status { get; set; }
    }
}