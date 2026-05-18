using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
        private const long MaxImageSizeBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];

         private readonly ApplicationDbContext _dbContext;
         private readonly UserManager<ApplicationUser> _userManager;
         private readonly ILogger<SetupProfileModel> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

         public SetupProfileModel(
             ApplicationDbContext dbContext,
             UserManager<ApplicationUser> userManager,
            ILogger<SetupProfileModel> logger,
            IWebHostEnvironment webHostEnvironment)
         {
             _dbContext = dbContext;
             _userManager = userManager;
             _logger = logger;
            _webHostEnvironment = webHostEnvironment;
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

            [Display(Name = "Avatar image")]
            public IFormFile? AvatarFile { get; set; }

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
            if (Input.AvatarFile is { Length: > 0 })
            {
                string? validationError = ValidateImageFile(Input.AvatarFile);
                if (validationError is not null)
                {
                    ModelState.AddModelError("Input.AvatarFile", validationError);
                }
            }

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
            profile.Intent = Input.Intent;
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            if (Input.AvatarFile is { Length: > 0 })
            {
                string avatarUrl = await SaveImageAsync(Input.AvatarFile, "avatars");
                profile.AvatarUrl = avatarUrl;
                user.ProfilePictureUrl = avatarUrl;
                _dbContext.Users.Update(user);
            }

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

        private static string? ValidateImageFile(IFormFile file)
        {
            if (!file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                return "Only image files are allowed.";
            }

            if (file.Length == 0)
            {
                return "The uploaded file is empty.";
            }

            if (file.Length > MaxImageSizeBytes)
            {
                return "The image must be 5 MB or smaller.";
            }

            string extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return "Only JPG, PNG, GIF, or WEBP images are allowed.";
            }

            return null;
        }

        private async Task<string> SaveImageAsync(IFormFile file, string folderName)
        {
            string uploadsRoot = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", folderName);
            Directory.CreateDirectory(uploadsRoot);

            string extension = Path.GetExtension(file.FileName);
            string fileName = $"{Guid.NewGuid():N}{extension}";
            string filePath = Path.Combine(uploadsRoot, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/{folderName}/{fileName}";
        }
    }
}