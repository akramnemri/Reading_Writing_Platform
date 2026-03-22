using System.ComponentModel.DataAnnotations;
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
    public class SetupProfileModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<SetupProfileModel> _logger;

        public SetupProfileModel(
            ApplicationDbContext dbContext,
            UserManager<IdentityUser> userManager,
            ILogger<SetupProfileModel> logger)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required, MaxLength(80)]
            [Display(Name = "Display name")]
            public string DisplayName { get; set; } = string.Empty;

            [MaxLength(500)]
            public string? Bio { get; set; }

            [MaxLength(500)]
            [Display(Name = "Avatar URL")]
            public string? AvatarUrl { get; set; }

            [Display(Name = "What brings you here?")]
            public ProfileIntent Intent { get; set; } = ProfileIntent.Read;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (profile is not null)
            {
                Input.DisplayName = profile.DisplayName;
                Input.Bio = profile.Bio;
                Input.AvatarUrl = profile.AvatarUrl;
                Input.Intent = profile.Intent;
            }
            else
            {
                Input.DisplayName = user.Email ?? "Reader";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user is null)
            {
                return Challenge();
            }

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(x => x.UserId == user.Id);
            if (profile is null)
            {
                profile = new UserProfile
                {
                    UserId = user.Id,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.UserProfiles.Add(profile);
            }

            profile.DisplayName = Input.DisplayName.Trim();
            profile.Bio = Input.Bio?.Trim();
            profile.AvatarUrl = Input.AvatarUrl?.Trim();
            profile.Intent = Input.Intent;
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            if (!await _userManager.IsInRoleAsync(user, RoleNames.Member))
            {
                await _userManager.AddToRoleAsync(user, RoleNames.Member);
            }

            bool needsAuthor = Input.Intent is ProfileIntent.Write or ProfileIntent.Both;
            bool isAuthor = await _userManager.IsInRoleAsync(user, RoleNames.Author);

            if (needsAuthor && !isAuthor)
            {
                await _userManager.AddToRoleAsync(user, RoleNames.Author);
            }

            if (!needsAuthor && isAuthor)
            {
                await _userManager.RemoveFromRoleAsync(user, RoleNames.Author);
            }

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Profile setup completed for user {UserId}. Intent: {Intent}", user.Id, Input.Intent);

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            {
                return LocalRedirect(ReturnUrl);
            }

            return RedirectToPage("/Index");
        }
    }
}