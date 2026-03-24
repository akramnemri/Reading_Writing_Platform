using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform;

public class UserLibrary
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public int NovelId { get; set; }
    public Novel Novel { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}