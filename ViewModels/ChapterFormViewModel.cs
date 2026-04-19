using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class ChapterFormViewModel
    {
        public Guid? Id { get; set; }
        public Guid NovelId { get; set; }
        public string NovelSlug { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public ChapterStatus Status { get; set; } = ChapterStatus.Draft;

        public bool IsLocked { get; set; }

        [Range(0, 100000)]
        public decimal BasePrice { get; set; }

        [Range(1, int.MaxValue)]
        public int Order { get; set; } = 1;

        public byte[]? RowVersion { get; set; }

        public NovelStatus NovelStatus { get; set; } = NovelStatus.Draft;

        public IEnumerable<SelectListItem> AvailableStatuses { get; set; } = new List<SelectListItem>();
    }
}