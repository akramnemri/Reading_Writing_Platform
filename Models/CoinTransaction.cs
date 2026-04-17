using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Reading_Writing_Platform.Models
{
    public enum CoinTransactionType
    {
        Purchase = 0,       // Achat de coins (Stripe/PayPal)
        ChapterUnlock = 1,  // Dépense pour déverrouiller un chapitre
        Refund = 2,         // Remboursement
        AdminAdjustment = 3 // Ajustement manuel admin
    }

    public class CoinTransaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string UserId { get; set; } = string.Empty;

        public IdentityUser? User { get; set; }

        public CoinTransactionType Type { get; set; }

        [Range(-1000000, 1000000)]
        public int Amount { get; set; }

        public int BalanceAfter { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public Guid? ChapterId { get; set; }
        public Chapter? Chapter { get; set; }

        [MaxLength(100)]
        public string? ExternalTransactionId { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}