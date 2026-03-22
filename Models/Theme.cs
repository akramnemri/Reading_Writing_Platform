using System.ComponentModel.DataAnnotations;

namespace Reading_Writing_Platform.Models
{
    public class Theme
    {
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Name { get; set; } = string.Empty;

        public ICollection<NovelTheme> NovelThemes { get; set; } = [];
    }
}