using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class NovelListItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public NovelStatus Status { get; set; }
        public int SubmissionCount { get; set; } = 0;
        public DateTimeOffset UpdatedAt { get; set; }
        public int ChapterCount { get; set; }
        public int? LastReadChapterNumber { get; set; }
    }
}
