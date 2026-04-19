using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Reading_Writing_Platform.Models
{
    public class Novel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string AuthorUserId { get; set; } = string.Empty;

        public IdentityUser? AuthorUser { get; set; }

        [Required, MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [Required, MaxLength(180)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? CoverImageUrl { get; set; }

        public NovelStatus Status { get; set; } = NovelStatus.Draft;

        public DateTimeOffset? ApprovedAt { get; set; }
        public DateTimeOffset? RejectedAt { get; set; }

        [MaxLength(500)]
        public string? RejectionReason { get; set; }

        public DateTimeOffset? SubmittedForReviewAt { get; set; }

        public int SubmissionCount { get; set; } = 0;

        public DateTimeOffset? PublishedAt { get; set; }

        // Review tracking
        public DateTimeOffset? ReviewedAt { get; set; }
        [MaxLength(450)]
        public string? ReviewedByUserId { get; set; }
        public IdentityUser? ReviewedByUser { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

        [Timestamp]
        public byte[] RowVersion { get; set; } = [];

        public ICollection<Chapter> Chapters { get; set; } = [];
        public ICollection<NovelTheme> NovelThemes { get; set; } = [];
        public ICollection<ReadingProgress> ReadingProgresses { get; set; } = [];

        
    }
}
