using System.ComponentModel.DataAnnotations;
using Reading_Writing_Platform;

namespace Reading_Writing_Platform.Models
{
    public class ReviewHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid NovelId { get; set; }

        public Novel? Novel { get; set; }

        [Required]
        public string Action { get; set; } = string.Empty; // "Submitted", "Approved", "Rejected", "Withdrawn", "Published", "Paused", etc.

        [MaxLength(1000)]
        public string? Notes { get; set; } // Rejection reason, etc.

        [Required]
        public string PerformedByUserId { get; set; } = string.Empty;

        public ApplicationUser? PerformedByUser { get; set; }

        public DateTimeOffset PerformedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
