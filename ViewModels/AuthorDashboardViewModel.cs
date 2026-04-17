using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class AuthorDashboardViewModel
    {
        public IReadOnlyList<AuthorDashboardNovelItemViewModel> Novels { get; set; } = [];

        public int TotalNovels { get; set; }
        public int PublishedNovels { get; set; }
        public int DraftNovels { get; set; }
        public int SubmittedNovels { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalPages { get; set; } = 1;
    }

    public class AuthorDashboardNovelItemViewModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public NovelStatus Status { get; set; }

        public int ChapterCount { get; set; }
        public int PublishedChapterCount { get; set; }
        public int DraftChapterCount { get; set; }
        public int TotalWordCount { get; set; }

        public int? ReadsCount { get; set; }
        public int? PurchasesCount { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }

        public bool CanSubmitForReview => Status == NovelStatus.Draft || Status == NovelStatus.Rejected;
    }
}