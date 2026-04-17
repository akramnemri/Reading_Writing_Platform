using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class NovelFormViewModel : IValidatableObject
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

        public byte[]? RowVersion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length < 3)
            {
                yield return new ValidationResult(
                    "Title must contain at least 3 non-space characters.",
                    [nameof(Title)]);
            }

            if (!string.IsNullOrWhiteSpace(CoverImageUrl))
            {
                bool validUrl = Uri.TryCreate(CoverImageUrl.Trim(), UriKind.Absolute, out Uri? uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

                if (!validUrl)
                {
                    yield return new ValidationResult(
                        "Cover image URL must be an absolute http/https URL.",
                        [nameof(CoverImageUrl)]);
                }
            }

            if ((Status == NovelStatus.Submitted || Status == NovelStatus.Published)
                && string.IsNullOrWhiteSpace(Description))
            {
                yield return new ValidationResult(
                    "Description is required before submitting or publishing a novel.",
                    [nameof(Description)]);
            }
        }
    }
}