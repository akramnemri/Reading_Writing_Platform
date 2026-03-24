using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform;

public class Volume
{
    public int Id { get; set; }
    public int NovelId { get; set; }
    public Novel Novel { get; set; } = null!;
    public int VolumeNumber { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}
