using System.Text;
using System.Text.RegularExpressions;

namespace Reading_Writing_Platform.Utilities
{
    public static partial class SlugHelper
    {
        public static string Generate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "novel";
            }

            string normalized = text.Trim().ToLowerInvariant();
            normalized = normalized.Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder();
            foreach (char c in normalized)
            {
                var unicodeCategory = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string withoutDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);
            string slug = NonAlphaNumericRegex().Replace(withoutDiacritics, "-").Trim('-');
            slug = MultiDashRegex().Replace(slug, "-");

            return string.IsNullOrWhiteSpace(slug) ? "novel" : slug;
        }

        [GeneratedRegex("[^a-z0-9-]+", RegexOptions.Compiled)]
        private static partial Regex NonAlphaNumericRegex();

        [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
        private static partial Regex MultiDashRegex();
    }
}