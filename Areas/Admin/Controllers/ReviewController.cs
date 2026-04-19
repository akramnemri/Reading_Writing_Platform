using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
using Reading_Writing_Platform.Areas.Admin.Models;
using System.Security.Claims;

namespace Reading_Writing_Platform.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = RoleNames.Admin)]
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;

        public ReviewController(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? status = null,
            int? themeId = null,
            string? authorId = null,
            string? search = null,
            int page = 1,
            int pageSize = 12)
        {
            // Base query (without includes) on reviewable statuses
            IQueryable<Novel> baseQuery = _dbContext.Novels
                .Where(x => x.Status == NovelStatus.Submitted || x.Status == NovelStatus.Rejected || x.Status == NovelStatus.Approved);

            // Apply filters
            if (themeId.HasValue)
                baseQuery = baseQuery.Where(x => x.NovelThemes.Any(nt => nt.ThemeId == themeId.Value));

            if (!string.IsNullOrWhiteSpace(authorId))
                baseQuery = baseQuery.Where(x => x.AuthorUserId == authorId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                string searchTerm = search.Trim();
                baseQuery = baseQuery.Where(x => x.Title.Contains(searchTerm) ||
                    (x.AuthorUser != null && x.AuthorUser.UserName != null && x.AuthorUser.UserName.Contains(searchTerm)));
            }

            // Compute status counts for filter pills (before applying status filter)
            var statusCounts = await baseQuery
                .GroupBy(x => x.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToDictionaryAsync(k => k.Status, v => v.Count);

            int totalAll = statusCounts.Values.Sum();
            int submittedCount = statusCounts.GetValueOrDefault(NovelStatus.Submitted);
            int rejectedCount = statusCounts.GetValueOrDefault(NovelStatus.Rejected);
            int approvedCount = statusCounts.GetValueOrDefault(NovelStatus.Approved);

            ViewBag.SubmittedCount = submittedCount;
            ViewBag.RejectedCount = rejectedCount;
            ViewBag.ApprovedCount = approvedCount;
            ViewBag.TotalAll = totalAll;

            // Apply status filter
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<NovelStatus>(status, out var statusEnum))
                baseQuery = baseQuery.Where(x => x.Status == statusEnum);

            int totalItems = await baseQuery.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            // Get paged novels with includes for display
            var novels = await baseQuery
                .Include(x => x.AuthorUser)
                .Include(x => x.NovelThemes)
                    .ThenInclude(x => x.Theme)
                .OrderByDescending(x => x.SubmittedForReviewAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Themes dropdown
            ViewBag.Themes = await _dbContext.Themes
                .Select(x => new SelectListItem { Text = x.Name, Value = x.Id.ToString() })
                .ToListAsync();

            // Authors dropdown based on current filtered baseQuery (includes all filters except paging)
            ViewBag.Authors = await baseQuery
                .Select(x => new
                {
                    x.AuthorUserId,
                    UserName = x.AuthorUser != null && x.AuthorUser.UserName != null
                        ? x.AuthorUser.UserName
                        : "Unknown"
                })
                .Distinct()
                .Select(x => new SelectListItem { Text = x.UserName, Value = x.AuthorUserId })
                .ToListAsync();

            ViewBag.CurrentSearch = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;

            return View(novels);
        }

        [HttpGet]
        public async Task<IActionResult> Details(Guid id)
        {
            var novel = await _dbContext.Novels
                .Include(x => x.AuthorUser)
                .Include(x => x.ReviewedByUser) // NEW
                .Include(x => x.NovelThemes)
                    .ThenInclude(x => x.Theme)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null) return NotFound();
            if (novel.Status != NovelStatus.Submitted && novel.Status != NovelStatus.Rejected && novel.Status != NovelStatus.Approved)
                return BadRequest("Novel is not in a reviewable state.");

            var chapters = await _dbContext.Chapters
                .Where(x => x.NovelId == id)
                .OrderBy(x => x.ChapterNumber)
                .Select(x => new ChapterListItemViewModel
                {
                    Id = x.Id,
                    ChapterNumber = x.ChapterNumber,
                    Title = x.Title,
                    WordCount = x.WordCount,
                    Status = x.Status
                })
                .ToListAsync();

            var authorProfile = await _dbContext.UserProfiles
                .FirstOrDefaultAsync(p => p.UserId == novel.AuthorUserId);

            var vm = new ReviewDetailsViewModel
            {
                Novel = novel,
                Chapters = chapters,
                RejectionReason = novel.RejectionReason ?? string.Empty,
                AuthorUserId = novel.AuthorUserId ?? string.Empty,
                AuthorBlockedUntil = authorProfile?.BlockedUntil?.ToLocalTime().ToString("f"),
                SubmissionCount = novel.SubmissionCount,
                ReviewedAt = novel.ReviewedAt,
                ReviewedByUserName = novel.ReviewedByUser?.UserName
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(Guid id)
        {
            var novel = await _dbContext.Novels.FindAsync(id);
            if (novel is null) return NotFound();
            if (novel.Status != NovelStatus.Submitted)
                return BadRequest("Only submitted novels can be approved. Rejected novels must be resubmitted first.");

            var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(reviewerId))
                return Forbid();

            novel.Status = NovelStatus.Approved;
            novel.ApprovedAt = DateTimeOffset.UtcNow;
            novel.ReviewedAt = DateTimeOffset.UtcNow;
            novel.ReviewedByUserId = reviewerId;
            novel.RejectedAt = null;
            novel.RejectionReason = null;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log review history
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Approved",
                PerformedByUserId = reviewerId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = null
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();

            // Grant Author role to the novel's author (if not already)
            var author = await _userManager.FindByIdAsync(novel.AuthorUserId);
            if (author != null && !await _userManager.IsInRoleAsync(author, RoleNames.Author))
            {
                await _userManager.AddToRoleAsync(author, RoleNames.Author);
            }

            TempData["SuccessMessage"] = $"✅ Approved! Author can now publish '{novel.Title}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(Guid id, string rejectionReason)
        {
            var novel = await _dbContext.Novels.FindAsync(id);
            if (novel is null) return NotFound();
            if (novel.Status != NovelStatus.Submitted && novel.Status != NovelStatus.Rejected)
                return BadRequest("Novel cannot be rejected in current state.");

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["ErrorMessage"] = "Rejection reason is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(reviewerId))
                return Forbid();

            novel.Status = NovelStatus.Rejected;
            novel.RejectedAt = DateTimeOffset.UtcNow;
            novel.ReviewedAt = DateTimeOffset.UtcNow;
            novel.ReviewedByUserId = reviewerId;
            novel.RejectionReason = rejectionReason.Trim();
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log review history
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Rejected",
                PerformedByUserId = reviewerId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = rejectionReason.Trim()
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BlockUser(string userId, int days, string reason)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User ID is required.");

            if (days <= 0 || days > 365)
                return BadRequest("Block duration must be between 1 and 365 days.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new UserProfile
                {
                    UserId = userId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.UserProfiles.Add(profile);
            }

            profile.BlockedUntil = DateTimeOffset.UtcNow.AddDays(days);
            profile.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"User blocked until {profile.BlockedUntil.Value.ToLocalTime():f}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnblockUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return BadRequest("User ID is required.");

            var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.BlockedUntil == null)
            {
                TempData["ErrorMessage"] = "User is not blocked.";
                return RedirectToAction(nameof(Index));
            }

            profile.BlockedUntil = null;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "User has been unblocked.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var novel = await _dbContext.Novels.FindAsync(id);
            if (novel is null) return NotFound();

            if (novel.Status != NovelStatus.Approved)
            {
                TempData["ErrorMessage"] = "Only approved novels can be published.";
                return RedirectToAction(nameof(Index));
            }

            novel.Status = NovelStatus.Published;
            novel.PublishedAt = DateTimeOffset.UtcNow;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Published",
                PerformedByUserId = adminId ?? string.Empty,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = "Published by admin"
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ '{novel.Title}' has been published by admin!";
            return RedirectToAction(nameof(Index));
        }
    }
}