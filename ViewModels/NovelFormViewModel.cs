using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class NovelFormViewModel
    {
        public Guid? Id { get; set; }

        [Required, MaxLength(160)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [MaxLength(500), Display(Name = "Cover image URL")]
        public string? CoverImageUrl { get; set; }

        public NovelStatus Status { get; set; } = NovelStatus.Draft;

        [Display(Name = "Themes")]
        public List<int> SelectedThemeIds { get; set; } = [];

        public List<SelectListItem> AvailableThemes { get; set; } = [];

        public List<SelectListItem> AvailableStatuses { get; set; } = [];

        public int SubmissionCount { get; set; } = 0;

        [Display(Name = "Rejection Reason")]
        public string? RejectionReason { get; set; }

        public byte[]? RowVersion { get; set; }
    }
}