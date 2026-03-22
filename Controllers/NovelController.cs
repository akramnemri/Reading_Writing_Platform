using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
using Reading_Writing_Platform.Utilities;
using Reading_Writing_Platform.ViewModels;

namespace Reading_Writing_Platform.Controllers
{
    [Authorize(Roles = $"{RoleNames.Author},{RoleNames.Admin}")]
    public class NovelController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public NovelController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            IQueryable<Novel> query = _dbContext.Novels
                .Include(x => x.NovelThemes)
                .ThenInclude(x => x.Theme)
                .Include(x => x.Chapters);

            if (!User.IsInRole(RoleNames.Admin))
            {
                string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(x => x.AuthorUserId == userId);
            }

            var novels = await query
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync();

            return View(novels);
        }

        public async Task<IActionResult> Details(Guid id, string tab = "overview", int chapterPage = 1, int pageSize = 5)
        {
            if (chapterPage < 1)
            {
                chapterPage = 1;
            }

            pageSize = Math.Clamp(pageSize, 3, 20);

            var novel = await QueryOwnedOrAdminNovels()
                .Include(x => x.NovelThemes)
                .ThenInclude(x => x.Theme)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

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
                AvailableThemes = await GetThemesSelectListAsync()
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
                Status = vm.Status,
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
                AvailableThemes = await GetThemesSelectListAsync(novel.NovelThemes.Select(x => x.ThemeId))
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
            novel.Status = vm.Status;
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

        private IQueryable<Novel> QueryOwnedOrAdminNovels()
        {
            if (User.IsInRole(RoleNames.Admin))
            {
                return _dbContext.Novels.AsQueryable();
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
    }
}