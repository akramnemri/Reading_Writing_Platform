using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Reading_Writing_Platform.Models
{
    public class UserWallet
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser? User { get; set; }

        [Range(0, 1000000)]
        public int CoinsBalance { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}