namespace Reading_Writing_Platform.Models;
public class Payout
{
    public int Id { get; set; }
    public string AuthorId { get; set; } = null!;
    public ApplicationUser Author { get; set; } = null!;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Paid
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}
