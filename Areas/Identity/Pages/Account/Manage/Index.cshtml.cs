using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;

namespace Reading_Writing_Platform.Areas.Identity.Pages.Account.Manage
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _dbContext;

        public IndexModel(UserManager<ApplicationUser> userManager, ApplicationDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public string DisplayName { get; set; } = string.Empty;
        public string? Bio { get; set; }
        public string? ProfilePictureUrl { get; set; }
        public string Email { get; set; } = string.Empty;
        public ProfileIntent Intent { get; set; }
        public bool IsAuthor { get; set; }

        // Stats
        public int TotalNovels { get; set; }
        public int PublishedNovels { get; set; }
        public int DraftNovels { get; set; }
        public int TotalChapters { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            DisplayName = user.UserName ?? string.Empty;
            Email = user.Email ?? string.Empty;

            // Load user profile
            var profile = await _dbContext.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (profile != null)
            {
                Bio = profile.Bio;
                DisplayName = profile.DisplayName;
                Intent = profile.Intent;
            }

            // Use user's profile picture if set, otherwise fall back to the profile avatar
            ProfilePictureUrl = string.IsNullOrWhiteSpace(user.ProfilePictureUrl)
                ? profile?.AvatarUrl
                : user.ProfilePictureUrl;

            // Check if user is an author via role
            IsAuthor = await _userManager.IsInRoleAsync(user, RoleNames.Author);

            if (IsAuthor)
            {
                var userId = user.Id;
                var novelsQuery = _dbContext.Novels.Where(n => n.AuthorUserId == userId);
                TotalNovels = await novelsQuery.CountAsync();
                PublishedNovels = await novelsQuery.CountAsync(n => n.Status == NovelStatus.Published);
                DraftNovels = await novelsQuery.CountAsync(n => n.Status == NovelStatus.Draft);
                TotalChapters = await _dbContext.Chapters
                    .CountAsync(c => c.Novel != null && c.Novel.AuthorUserId == userId);
            }

            return Page();
        }
    }
}
