using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform;

public class Purchase
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public int ChapterId { get; set; }
    public Chapter Chapter { get; set; } = null!;
    public decimal Amount { get; set; }
    public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;
}