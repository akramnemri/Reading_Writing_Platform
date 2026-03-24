using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform;

public class NovelReview
{
    public int Id { get; set; }
    public int NovelId { get; set; }
    public Novel Novel { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public int Rating { get; set; }                        // 1-5
    public string? Title { get; set; }
    public string Text { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}