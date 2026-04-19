namespace Reading_Writing_Platform.Models
{
    public enum NovelStatus
    {
        Draft = 0,       // Author editing (visible only to author)
        Submitted = 1,   // Submitted for admin review
        Approved = 2,    // Admin approved, waiting for author to publish
        Rejected = 3,    // Admin rejected, back to author with reason
        Published = 4,   // Live and public
        Paused = 5,      // Temporarily paused (still published but no new chapters)
        Dropped = 6,     // Abandoned/Discontinued
        Completed = 7    // Finished - no more chapters allowed
    }
}
