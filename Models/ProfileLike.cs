namespace Reading_Writing_Platform;

public class ProfileLike
{
    public int Id { get; set; }
    public string LikerUserId { get; set; } = null!;       // who liked
    public ApplicationUser Liker { get; set; } = null!;
    public string LikedUserId { get; set; } = null!;       // whose profile was liked
    public ApplicationUser LikedUser { get; set; } = null!;
    public DateTime LikedAt { get; set; } = DateTime.UtcNow;
}