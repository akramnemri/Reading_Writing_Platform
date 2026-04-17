using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Reading_Writing_Platform.Models
{
    public class ChapterEntitlement
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ChapterId { get; set; }

        public Chapter? Chapter { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser? User { get; set; }

        public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;

        [Range(0, 10000)]
        public int CoinsCost { get; set; }

        public Guid? TransactionId { get; set; }
        public CoinTransaction? Transaction { get; set; }
    }
}