using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class NovelDetailsViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; }
        public NovelStatus Status { get; set; }

        public int TotalChapters { get; set; }
        public int PublishedChapters { get; set; }
        public int DraftChapters { get; set; }
        public int PaidChapters { get; set; }
        public int TotalWords { get; set; }

        public int? ReadsCount { get; set; }
        public int? PurchasesCount { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public IReadOnlyList<NovelDetailsChapterItemViewModel> Chapters { get; set; } = Array.Empty<NovelDetailsChapterItemViewModel>();

        public int ChapterPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;

        public bool CanSubmitForReview => Status == NovelStatus.Draft || Status == NovelStatus.Rejected;
    }

    public class NovelDetailsChapterItemViewModel
    {
        public Guid Id { get; set; }
        public int ChapterNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public ChapterStatus Status { get; set; }
        public bool IsLocked { get; set; }
        public int BasePrice { get; set; }
        public int WordCount { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public int? PurchasesCount { get; set; }
    }
}