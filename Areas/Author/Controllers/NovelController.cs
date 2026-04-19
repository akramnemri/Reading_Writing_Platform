using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
using Reading_Writing_Platform.Utilities;
using Reading_Writing_Platform.ViewModels;

namespace Reading_Writing_Platform.Areas.Author.Controllers
{
    [Area("Author")]
    [Authorize(Roles = RoleNames.Member + "," + RoleNames.Admin)]
    public class NovelController : Controller
    {
        private const int DefaultPageNumber = 1;
        private const int MinimumPageSize = 3;
        private const int MaximumPageSize = 20;

        private readonly ApplicationDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;

        public NovelController(ApplicationDbContext dbContext, UserManager<IdentityUser> userManager)
        {
            _dbContext = dbContext;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string? status = null)
        {
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IQueryable<Novel> query = _dbContext.Novels.Where(x => x.AuthorUserId == userId);

            // Compute total counts before filtering
            var totalCounts = await query
                .GroupBy(x => 1)
                .Select(g => new
                {
                    Total = g.Count(),
                    Published = g.Count(x => x.Status == NovelStatus.Published),
                    Draft = g.Count(x => x.Status == NovelStatus.Draft)
                })
                .FirstOrDefaultAsync();

            ViewBag.TotalNovels = totalCounts?.Total ?? 0;
            ViewBag.PublishedCount = totalCounts?.Published ?? 0;
            ViewBag.DraftCount = totalCounts?.Draft ?? 0;

            // Apply status filter if provided
            if (!string.IsNullOrWhiteSpace(status))
            {
                if (Enum.TryParse<NovelStatus>(status, out var statusEnum))
                {
                    query = query.Where(x => x.Status == statusEnum);
                }
            }

            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var novelsQuery = query
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x => new NovelListItemViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Description = x.Description,
                    CoverImageUrl = x.CoverImageUrl,
                    Status = x.Status,
                    SubmissionCount = x.SubmissionCount,
                    UpdatedAt = x.UpdatedAt,
                    ChapterCount = x.Chapters.Count,
                    LastReadChapterNumber = currentUserId != null
                        ? x.ReadingProgresses
                            .Where(rp => rp.UserId == currentUserId)
                            .Select(rp => (int?)rp.LastReadChapterNumber)
                            .FirstOrDefault()
                        : null
                });

            var novels = await novelsQuery.ToListAsync();

            ViewBag.CurrentStatus = status;

            return View(novels);
        }

        public async Task<IActionResult> Details(Guid id, string tab = "overview", int chapterPage = 1, int pageSize = 5)
        {
            if (chapterPage < 1)
            {
                chapterPage = DefaultPageNumber;
            }

            pageSize = Math.Clamp(pageSize, MinimumPageSize, MaximumPageSize);

            var novel = await QueryOwnedOrAdminNovels()
                .Include(x => x.NovelThemes)
                .ThenInclude(x => x.Theme)
                .Include(x => x.ReviewedByUser) // for reviewer name
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            // Load review history for author to see
            var reviewHistory = await _dbContext.ReviewHistories
                .Include(rh => rh.PerformedByUser)
                .Where(rh => rh.NovelId == id)
                .OrderByDescending(rh => rh.PerformedAt)
                .ToListAsync();

            ViewBag.ReviewHistory = reviewHistory;

            var chaptersQuery = _dbContext.Chapters
                .AsNoTracking()
                .Where(x => x.NovelId == id);

            int totalChapters = await chaptersQuery.CountAsync();
            int publishedChapters = await chaptersQuery.CountAsync(x => x.Status == ChapterStatus.Published);
            int draftChapters = await chaptersQuery.CountAsync(x => x.Status == ChapterStatus.Draft);
            int totalWords = await chaptersQuery.SumAsync(x => (int?)x.WordCount) ?? 0;
            int paidChapters = await chaptersQuery.CountAsync(x => x.BasePrice > 0);

            decimal avgPrice = paidChapters == 0
                ? 0m
                : await chaptersQuery.Where(x => x.BasePrice > 0).AverageAsync(x => x.BasePrice);

            var chartData = await chaptersQuery
                .OrderByDescending(x => x.ChapterNumber)
                .Select(x => x.WordCount)
                .Take(7)
                .ToListAsync();
            chartData.Reverse();

            var recentChapters = await chaptersQuery
                .OrderByDescending(x => x.ChapterNumber)
                .Take(3)
                .ToListAsync();

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalChapters / (double)pageSize));
            if (chapterPage > totalPages)
            {
                chapterPage = totalPages;
            }

            var pagedChapters = await chaptersQuery
                .OrderByDescending(x => x.ChapterNumber)
                .Skip((chapterPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(tab) ? "overview" : tab.ToLowerInvariant();

            ViewBag.TotalChapters = totalChapters;
            ViewBag.PublishedChapters = publishedChapters;
            ViewBag.DraftChapters = draftChapters;
            ViewBag.TotalWords = totalWords;
            ViewBag.PaidChapters = paidChapters;
            ViewBag.AvgPrice = avgPrice;

            ViewBag.ChartData = chartData;
            ViewBag.RecentChapters = recentChapters;

            ViewBag.PagedChapters = pagedChapters;
            ViewBag.ChapterPage = chapterPage;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;

            return View(novel);
        }

        public async Task<IActionResult> Create()
        {
            var vm = new NovelFormViewModel
            {
                AvailableThemes = await GetThemesSelectListAsync(),
                AvailableStatuses = new List<SelectListItem>
                {
                    new SelectListItem { Text = NovelStatus.Draft.ToString(), Value = ((int)NovelStatus.Draft).ToString(), Selected = true }
                }
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NovelFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                vm.AvailableThemes = await GetThemesSelectListAsync(vm.SelectedThemeIds);
                return View(vm);
            }

            string authorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(authorUserId))
            {
                return Forbid();
            }

            string baseSlug = SlugHelper.Generate(vm.Title);
            string uniqueSlug = await GetUniqueSlugAsync(baseSlug);

            var novel = new Novel
            {
                AuthorUserId = authorUserId,
                Title = vm.Title.Trim(),
                Slug = uniqueSlug,
                Description = vm.Description?.Trim(),
                CoverImageUrl = vm.CoverImageUrl?.Trim(),
                Status = NovelStatus.Draft, // Always start as Draft
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            foreach (int themeId in vm.SelectedThemeIds.Distinct())
            {
                novel.NovelThemes.Add(new NovelTheme { ThemeId = themeId });
            }

            _dbContext.Novels.Add(novel);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels()
                .Include(x => x.NovelThemes)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            var vm = new NovelFormViewModel
            {
                Id = novel.Id,
                Title = novel.Title,
                Description = novel.Description,
                CoverImageUrl = novel.CoverImageUrl,
                Status = novel.Status,
                SelectedThemeIds = novel.NovelThemes.Select(x => x.ThemeId).ToList(),
                RowVersion = novel.RowVersion,
                AvailableThemes = await GetThemesSelectListAsync(novel.NovelThemes.Select(x => x.ThemeId)),
                SubmissionCount = novel.SubmissionCount,
                RejectionReason = novel.RejectionReason
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, NovelFormViewModel vm)
        {
            if (id != vm.Id)
            {
                return BadRequest();
            }

            var novel = await QueryOwnedOrAdminNovels()
                .Include(x => x.NovelThemes)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                vm.AvailableThemes = await GetThemesSelectListAsync(vm.SelectedThemeIds);
                return View(vm);
            }

            string newBaseSlug = SlugHelper.Generate(vm.Title);
            string newUniqueSlug = await GetUniqueSlugAsync(newBaseSlug, novel.Id);

            novel.Title = vm.Title.Trim();
            novel.Description = vm.Description?.Trim();
            novel.CoverImageUrl = vm.CoverImageUrl?.Trim();
            // DO NOT change Status here - it's managed by dedicated actions
            // novel.Status = vm.Status;
            novel.Slug = newUniqueSlug;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            _dbContext.Entry(novel).Property(x => x.RowVersion).OriginalValue = vm.RowVersion ?? [];

            novel.NovelThemes.Clear();
            foreach (int themeId in vm.SelectedThemeIds.Distinct())
            {
                novel.NovelThemes.Add(new NovelTheme
                {
                    NovelId = novel.Id,
                    ThemeId = themeId
                });
            }

            try
            {
                await _dbContext.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "This novel was modified by another user. Reload and try again.");
                vm.AvailableThemes = await GetThemesSelectListAsync(vm.SelectedThemeIds);
                vm.AvailableStatuses = GetAllowedStatusSelectList(novel.Status);
                return View(vm);
            }
        }

        public async Task<IActionResult> Delete(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            return View(novel);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels()
                .Include(x => x.Chapters)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            _dbContext.Novels.Remove(novel);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels().FirstOrDefaultAsync(x => x.Id == id);
            if (novel is null) return NotFound();

            if (novel.Status != NovelStatus.Draft && novel.Status != NovelStatus.Rejected)
            {
                TempData["ErrorMessage"] = "Only drafts or rejected novels can be submitted for review.";
                return RedirectToAction(nameof(Index));
            }

            // Check if user is blocked
            var user = await _userManager.FindByIdAsync(novel.AuthorUserId);
            if (user != null)
            {
                var profile = await _dbContext.UserProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
                if (profile?.BlockedUntil.HasValue == true && profile.BlockedUntil > DateTimeOffset.UtcNow)
                {
                    TempData["ErrorMessage"] = $"You are blocked from submitting until {profile.BlockedUntil.Value.ToLocalTime():f}.";
                    return RedirectToAction(nameof(Index));
                }
            }

            novel.Status = NovelStatus.Submitted;
            novel.SubmittedForReviewAt = DateTimeOffset.UtcNow;
            novel.SubmissionCount++;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log submission history
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Submitted",
                PerformedByUserId = novel.AuthorUserId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = null
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Novel submitted for review successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Withdraw(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels().FirstOrDefaultAsync(x => x.Id == id);
            if (novel is null) return NotFound();

            if (novel.Status != NovelStatus.Submitted)
            {
                TempData["ErrorMessage"] = "Only submitted novels can be withdrawn.";
                return RedirectToAction(nameof(Index));
            }

            novel.Status = NovelStatus.Draft;
            novel.SubmittedForReviewAt = null;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log withdrawal history
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Withdrawn",
                PerformedByUserId = userId ?? novel.AuthorUserId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = "Author withdrew submission"
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "Novel withdrawn. It is now in Draft status.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActiveStatus(Guid id, NovelStatus status)
        {
            var novel = await QueryOwnedOrAdminNovels().FirstOrDefaultAsync(x => x.Id == id);
            if (novel is null) return NotFound();

            if (novel.Status != NovelStatus.Published)
            {
                TempData["ErrorMessage"] = "Only published novels can change active status.";
                return RedirectToAction(nameof(Index));
            }

            // Allowed transitions from Published: Paused, Dropped, Completed
            if (status != NovelStatus.Paused && status != NovelStatus.Dropped && status != NovelStatus.Completed)
            {
                TempData["ErrorMessage"] = "Invalid status transition.";
                return RedirectToAction(nameof(Index));
            }

            novel.Status = status;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log status change history
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = status.ToString(),
                PerformedByUserId = userId ?? novel.AuthorUserId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = $"Novel status changed to {status} by author"
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Novel status changed to {status}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid id)
        {
            var novel = await QueryOwnedOrAdminNovels().FirstOrDefaultAsync(x => x.Id == id);
            if (novel is null) return NotFound();

            if (novel.Status != NovelStatus.Approved)
            {
                TempData["ErrorMessage"] = "Novel must be approved before publishing.";
                return RedirectToAction(nameof(Index));
            }

            novel.Status = NovelStatus.Published;
            novel.PublishedAt = DateTimeOffset.UtcNow;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            // Log publish history
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var history = new ReviewHistory
            {
                NovelId = novel.Id,
                Action = "Published",
                PerformedByUserId = userId ?? novel.AuthorUserId,
                PerformedAt = DateTimeOffset.UtcNow,
                Notes = "Novel published by author"
            };
            _dbContext.ReviewHistories.Add(history);

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"'{novel.Title}' has been published!";
            return RedirectToAction(nameof(Index));
        }


        private IQueryable<Novel> QueryOwnedOrAdminNovels()
        {
            if (User.IsInRole(RoleNames.Admin))
            {
                return _dbContext.Novels;
            }
            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return _dbContext.Novels.Where(x => x.AuthorUserId == userId);
        }

        private async Task<List<SelectListItem>> GetThemesSelectListAsync(IEnumerable<int>? selectedIds = null)
        {
            var selected = selectedIds?.ToHashSet() ?? [];
            var themes = await _dbContext.Themes
                .OrderBy(x => x.Name)
                .ToListAsync();

            return themes.Select(x => new SelectListItem
            {
                Value = x.Id.ToString(),
                Text = x.Name,
                Selected = selected.Contains(x.Id)
            }).ToList();
        }

        private async Task<string> GetUniqueSlugAsync(string baseSlug, Guid? ignoreNovelId = null)
        {
            string slug = baseSlug;
            int suffix = 2;

            while (await _dbContext.Novels.AnyAsync(x => x.Slug == slug && (!ignoreNovelId.HasValue || x.Id != ignoreNovelId.Value)))
            {
                slug = $"{baseSlug}-{suffix}";
                suffix++;
            }

            return slug;
        }

        private static HashSet<NovelStatus> GetAllowedStatusTransitions(NovelStatus current)
        {
            var allowed = new HashSet<NovelStatus> { current }; // always keep current
            return current switch
            {
                NovelStatus.Draft => [.. allowed, NovelStatus.Submitted],
                NovelStatus.Submitted => [.. allowed, NovelStatus.Draft], // withdraw
                NovelStatus.Approved => [.. allowed, NovelStatus.Published, NovelStatus.Draft],
                NovelStatus.Rejected => [.. allowed, NovelStatus.Submitted], // must resubmit directly
                NovelStatus.Published => [.. allowed, NovelStatus.Paused, NovelStatus.Dropped, NovelStatus.Completed],
                NovelStatus.Paused => [.. allowed, NovelStatus.Published],
                NovelStatus.Dropped => allowed, // locked
                NovelStatus.Completed => allowed, // locked
                _ => allowed
            };
        }

        private static string ValidateStatusTransition(NovelStatus from, NovelStatus to)
        {
            if (from == to) return string.Empty;

            return from switch
            {
                NovelStatus.Draft => to == NovelStatus.Submitted
                    ? string.Empty
                    : "Draft can only be submitted for review (Submitted) or kept as Draft.",

                NovelStatus.Submitted => to == NovelStatus.Draft
                    ? string.Empty
                    : "Submitted novels can only be withdrawn back to Draft. Contact an admin for status changes.",

                NovelStatus.Approved => to is NovelStatus.Published or NovelStatus.Draft
                    ? string.Empty
                    : "Approved novels can only be published or returned to Draft.",

                NovelStatus.Rejected => to == NovelStatus.Submitted
                    ? string.Empty
                    : "Rejected novels can only be resubmitted for review.",

                NovelStatus.Published => to is NovelStatus.Paused or NovelStatus.Dropped or NovelStatus.Completed or NovelStatus.Published
                    ? string.Empty
                    : "Published novels can only be set to Paused, Dropped, or Completed.",

                NovelStatus.Paused => to == NovelStatus.Published
                    ? string.Empty
                    : "Paused novels can only be resumed to Published.",

                NovelStatus.Dropped => "Dropped novels are locked and cannot be changed.",

                NovelStatus.Completed => "Completed novels are permanently locked and cannot be changed.",

                _ => "Invalid status transition."
            };
        }

        private List<SelectListItem> GetAllowedStatusSelectList(NovelStatus current)
        {
            var allowed = GetAllowedStatusTransitions(current);
            return Enum.GetValues<NovelStatus>()
                .Where(s => allowed.Contains(s) && s != NovelStatus.Submitted && s != NovelStatus.Published)
                .Select(s => new SelectListItem
                {
                    Text = s.ToString(),
                    Value = ((int)s).ToString(),
                    Selected = s == current
                })
                .ToList();
        }
    }
}