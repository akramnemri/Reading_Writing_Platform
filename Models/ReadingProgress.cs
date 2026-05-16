using System.ComponentModel.DataAnnotations;
using Reading_Writing_Platform;

namespace Reading_Writing_Platform.Models
{
    public class ReadingProgress
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser? User { get; set; }

        [Required]
        public Guid NovelId { get; set; }

        public Novel? Novel { get; set; }

        [Required]
        public int LastReadChapterNumber { get; set; }

        public DateTimeOffset LastReadAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
