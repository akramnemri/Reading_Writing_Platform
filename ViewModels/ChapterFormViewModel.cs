using System.ComponentModel.DataAnnotations;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.ViewModels
{
    public class ChapterFormViewModel : IValidatableObject
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

        [Range(0, 10000)]
        [Display(Name = "Price (Coins)")]
        public int BasePrice { get; set; }

        [Range(0, 10000)]
        [Display(Name = "Preview character count")]
        public int PreviewCharCount { get; set; } = 500;

        [Range(1, int.MaxValue)]
        public int Order { get; set; } = 1;

        public byte[]? RowVersion { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length < 2)
            {
                yield return new ValidationResult("Title must contain at least 2 non-space characters.", [nameof(Title)]);
            }

            if (IsLocked && BasePrice <= 0)
            {
                yield return new ValidationResult("Locked chapters must have a price greater than 0.", [nameof(BasePrice), nameof(IsLocked)]);
            }

            if (!IsLocked && BasePrice > 0)
            {
                yield return new ValidationResult("Set chapter as locked when price is greater than 0.", [nameof(BasePrice), nameof(IsLocked)]);
            }

            if (IsLocked && PreviewCharCount <= 0)
            {
                yield return new ValidationResult("Locked chapters must expose a preview length greater than 0.", [nameof(PreviewCharCount)]);
            }

            if (IsLocked && !string.IsNullOrWhiteSpace(Content) && PreviewCharCount >= Content.Length)
            {
                yield return new ValidationResult("Preview character count must be smaller than full content length for locked chapters.", [nameof(PreviewCharCount), nameof(Content)]);
            }

            if (Status == ChapterStatus.Published && CountWords(Content) < 50)
            {
                yield return new ValidationResult("Published chapters must contain at least 50 words.", [nameof(Content), nameof(Status)]);
            }
        }

        private static int CountWords(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            return content.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}