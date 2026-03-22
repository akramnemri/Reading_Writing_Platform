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

        public bool IsLocked { get; set; }
        public decimal BasePrice { get; set; }

        public int WordCount { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];
    }
}
