using System.ComponentModel.DataAnnotations;

namespace Reading_Writing_Platform.Models
{
    public class Chapter
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid NovelId { get; set; }

        public Novel? Novel { get; set; }

        public int ChapterNumber { get; set; }
        public int Order { get; set; }

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public ChapterStatus Status { get; set; } = ChapterStatus.Draft;
        public DateTimeOffset? PublishedAt { get; set; }

        // Locked chapter fields (Coins-based)
        public bool IsLocked { get; set; }

        [Range(0, 10000)]
        [Display(Name = "Price (Coins)")]
        public int BasePrice { get; set; }

        [Range(0, 10000)]
        [Display(Name = "Preview character count")]
        public int PreviewCharCount { get; set; } = 500;

        public int WordCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        // Helper methods
        public string GetPreviewContent()
        {
            if (string.IsNullOrWhiteSpace(Content))
            {
                return string.Empty;
            }

            if (!IsLocked || PreviewCharCount <= 0)
            {
                return Content;
            }

            if (Content.Length <= PreviewCharCount)
            {
                return Content;
            }

            return Content[..PreviewCharCount] + "...";
        }

        public bool CanUserAccess(string? userId, ICollection<ChapterEntitlement>? entitlements)
        {
            if (!IsLocked)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            if (entitlements == null || !entitlements.Any())
            {
                return false;
            }

            return entitlements.Any(e =>
                e.ChapterId == Id &&
                e.UserId == userId &&
                e.GrantedAt <= DateTimeOffset.UtcNow);
        }
    }
}
