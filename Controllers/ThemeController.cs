using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform;

namespace Reading_Writing_Platform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [IgnoreAntiforgeryToken]
    public class ThemeController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ThemeController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public class ThemeRequest
        {
            public string Theme { get; set; } = "light";
        }

        [HttpPost("Set")]
        public async Task<IActionResult> Set([FromBody] ThemeRequest request)
        {
            var theme = request.Theme == "dark" ? "dark" : "light";

            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    user.ThemePreference = theme;
                    await _userManager.UpdateAsync(user);
                }
            }

            // Set cookie for both authenticated and guests
            Response.Cookies.Append(
                "theme",
                theme,
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    Path = "/"
                });

            return Ok(new { success = true });
        }
    }
}
