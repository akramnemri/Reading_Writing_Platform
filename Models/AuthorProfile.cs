namespace Reading_Writing_Platform;

public class AuthorProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public string PenName { get; set; } = null!;
    public decimal TotalEarnings { get; set; } = 0;
    public int TotalLibraryAdds { get; set; } = 0;      // how many readers added his novels
    public int TotalViews { get; set; } = 0;
}
