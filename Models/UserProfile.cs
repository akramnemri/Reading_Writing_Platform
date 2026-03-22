using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Reading_Writing_Platform.Models
{
    public class UserProfile
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser? User { get; set; }

        [Required, MaxLength(80)]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Bio { get; set; }

        [MaxLength(500)]
        public string? AvatarUrl { get; set; }

        public ProfileIntent Intent { get; set; } = ProfileIntent.Read;

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}