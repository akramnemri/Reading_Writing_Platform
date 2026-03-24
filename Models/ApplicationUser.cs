using Microsoft.AspNetCore.Identity;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? ProfilePictureUrl { get; set; }          // profile pic
    public string? CoverPhotoUrl { get; set; }              // Facebook-style banner/landscape pic
    public string? Country { get; set; }                    // for location distribution
    public int TotalBooksRead { get; set; } = 0;
    public int TotalReadingMinutes { get; set; } = 0;       // reading time
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Author-only fields (null for readers)
    public bool IsAuthor { get; set; } = false;
    public int WritingStreakDays { get; set; } = 0;         // consecutive update days
    public DateTime? LastChapterUpdateDate { get; set; }

    // Navigation
    public AuthorProfile? AuthorProfile { get; set; }
    public ICollection<Novel> AuthoredNovels { get; set; } = new List<Novel>();
    public ICollection<UserLibrary> Library { get; set; } = new List<UserLibrary>();
    public ICollection<ChapterLike> ChapterLikes { get; set; } = new List<ChapterLike>();
    public ICollection<ProfileLike> LikedBy { get; set; } = new List<ProfileLike>();           // people who liked THIS profile
    public ICollection<ProfileLike> LikesGiven { get; set; } = new List<ProfileLike>();        // profiles THIS user liked
    public ICollection<NovelReview> ReviewsWritten { get; set; } = new List<NovelReview>();
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public ICollection<Report> ReportsMade { get; set; } = new List<Report>();
    public ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();
}