namespace Reading_Writing_Platform;

public class Report
{
    public int Id { get; set; }
    public string ReporterId { get; set; } = null!;
    public ApplicationUser Reporter { get; set; } = null!;
    public int? NovelId { get; set; }
    public int? ChapterId { get; set; }
    public int? CommentId { get; set; }
    public int? UserId { get; set; }                     // new: report a user
    public string Reason { get; set; } = null!;
    public bool IsResolved { get; set; } = false;
    public string? AdminNote { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public object Comment { get; internal set; }
    public object Chapter { get; internal set; }
    public object Novel { get; internal set; }
}
