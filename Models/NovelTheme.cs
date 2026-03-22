namespace Reading_Writing_Platform.Models
{
    public class NovelTheme
    {
        public Guid NovelId { get; set; }
        public Novel? Novel { get; set; }

        public int ThemeId { get; set; }
        public Theme? Theme { get; set; }
    }
}