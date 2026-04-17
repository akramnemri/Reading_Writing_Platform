using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
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

        public async Task<IActionResult> Index(int page = 1, int pageSize = 10)
        {
            if (page < 1)
            {
                page = 1;
            }

            pageSize = Math.Clamp(pageSize, 5, 30);

            IQueryable<Novel> novelsQuery = QueryOwnedOrAdminNovels()
                .AsNoTracking();

            int totalNovels = await novelsQuery.CountAsync();
            int publishedNovels = await novelsQuery.CountAsync(x => x.Status == NovelStatus.Published);
            int draftNovels = await novelsQuery.CountAsync(x => x.Status == NovelStatus.Draft);
            int submittedNovels = await novelsQuery.CountAsync(x => x.Status == NovelStatus.Submitted);

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalNovels / (double)pageSize));
            if (page > totalPages)
            {
                page = totalPages;
            }

            var novelItems = await novelsQuery
                .OrderByDescending(x => x.UpdatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new AuthorDashboardNovelItemViewModel
                {
                    Id = x.Id,
                    Title = x.Title,
                    Slug = x.Slug,
                    Status = x.Status,
                    ChapterCount = x.Chapters.Count,
                    PublishedChapterCount = x.Chapters.Count(c => c.Status == ChapterStatus.Published),
                    DraftChapterCount = x.Chapters.Count(c => c.Status == ChapterStatus.Draft),
                    TotalWordCount = x.Chapters.Sum(c => (int?)c.WordCount) ?? 0,
                    ReadsCount = null,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();

            var novelIds = novelItems.Select(x => x.Id).ToList();

            Dictionary<Guid, int> purchasesByNovel = [];
            if (novelIds.Count > 0)
            {
                purchasesByNovel = await (
                    from entitlement in _dbContext.ChapterEntitlements.AsNoTracking()
                    join chapter in _dbContext.Chapters.AsNoTracking()
                        on entitlement.ChapterId equals chapter.Id
                    where novelIds.Contains(chapter.NovelId)
                    group entitlement by chapter.NovelId
                    into grouped
                    select new
                    {
                        NovelId = grouped.Key,
                        Count = grouped.Count()
                    })
                    .ToDictionaryAsync(x => x.NovelId, x => x.Count);
            }

            foreach (var item in novelItems)
            {
                item.PurchasesCount = purchasesByNovel.GetValueOrDefault(item.Id, 0);
            }

            var vm = new AuthorDashboardViewModel
            {
                Novels = novelItems,
                TotalNovels = totalNovels,
                PublishedNovels = publishedNovels,
                DraftNovels = draftNovels,
                SubmittedNovels = submittedNovels,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return View(vm);
        }

        public async Task<IActionResult> Details(Guid id, int chapterPage = 1, int pageSize = 10)
        {
            if (chapterPage < 1)
            {
                chapterPage = 1;
            }

            pageSize = Math.Clamp(pageSize, 5, 30);

            var novelHeader = await QueryOwnedOrAdminNovels()
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.Title,
                    x.Slug,
                    x.Description,
                    x.CoverImageUrl,
                    x.Status,
                    x.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (novelHeader is null)
            {
                return NotFound();
            }

            IQueryable<Chapter> chaptersQuery = _dbContext.Chapters
                .AsNoTracking()
                .Where(x => x.NovelId == id);

            int totalChapters = await chaptersQuery.CountAsync();
            int publishedChapters = await chaptersQuery.CountAsync(x => x.Status == ChapterStatus.Published);
            int draftChapters = await chaptersQuery.CountAsync(x => x.Status == ChapterStatus.Draft);
            int paidChapters = await chaptersQuery.CountAsync(x => x.BasePrice > 0);
            int totalWords = await chaptersQuery.SumAsync(x => (int?)x.WordCount) ?? 0;

            int totalPages = Math.Max(1, (int)Math.Ceiling(totalChapters / (double)pageSize));
            if (chapterPage > totalPages)
            {
                chapterPage = totalPages;
            }

            var chapterItems = await chaptersQuery
                .OrderByDescending(x => x.ChapterNumber)
                .Skip((chapterPage - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new NovelDetailsChapterItemViewModel
                {
                    Id = x.Id,
                    ChapterNumber = x.ChapterNumber,
                    Title = x.Title,
                    Status = x.Status,
                    IsLocked = x.IsLocked,
                    BasePrice = x.BasePrice,
                    WordCount = x.WordCount,
                    UpdatedAt = x.UpdatedAt,
                    PurchasesCount = _dbContext.ChapterEntitlements.Count(e => e.ChapterId == x.Id)
                })
                .ToListAsync();

            int purchasesCount = await (
                from entitlement in _dbContext.ChapterEntitlements.AsNoTracking()
                join chapter in _dbContext.Chapters.AsNoTracking()
                    on entitlement.ChapterId equals chapter.Id
                where chapter.NovelId == id
                select entitlement.Id)
                .CountAsync();

            var vm = new NovelDetailsViewModel
            {
                Id = novelHeader.Id,
                Title = novelHeader.Title,
                Slug = novelHeader.Slug,
                Description = novelHeader.Description,
                CoverImageUrl = novelHeader.CoverImageUrl,
                Status = novelHeader.Status,
                UpdatedAt = novelHeader.UpdatedAt,
                TotalChapters = totalChapters,
                PublishedChapters = publishedChapters,
                DraftChapters = draftChapters,
                PaidChapters = paidChapters,
                TotalWords = totalWords,
                ReadsCount = null,
                PurchasesCount = purchasesCount,
                Chapters = chapterItems,
                ChapterPage = chapterPage,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitForReview(Guid id, string? returnUrl = null)
        {
            var novel = await QueryOwnedOrAdminNovels()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (novel is null)
            {
                return NotFound();
            }

            if (novel.Status != NovelStatus.Draft && novel.Status != NovelStatus.Rejected)
            {
                TempData["StatusMessage"] = "Only draft or rejected novels can be submitted for review.";
                return RedirectToLocalOrDefault(returnUrl, novel.Id);
            }

            novel.Status = NovelStatus.Submitted;
            novel.UpdatedAt = DateTimeOffset.UtcNow;

            await _dbContext.SaveChangesAsync();

            TempData["StatusMessage"] = "Novel submitted for review.";
            return RedirectToLocalOrDefault(returnUrl, novel.Id);
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
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.SingleOrDefault();
                if (entry?.Entity is Novel)
                {
                    var databaseValues = await entry.GetDatabaseValuesAsync();
                    if (databaseValues is null)
                    {
                        ModelState.AddModelError(string.Empty, "This novel was deleted by another user.");
                    }
                    else
                    {
                        var databaseNovel = (Novel)databaseValues.ToObject();
                        vm.RowVersion = databaseNovel.RowVersion;
                        ModelState.AddModelError(string.Empty, "This novel was modified by another user. Reload and apply your changes again.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "A concurrency error occurred. Reload and try again.");
                }

                vm.AvailableThemes = await GetThemesSelectListAsync(vm.SelectedThemeIds);
                return View(vm);
            }
            catch (DbUpdateException ex) when (IsSqlServerUniqueConstraintViolation(ex, "IX_Novels_Slug"))
            {
                ModelState.AddModelError(nameof(vm.Title), "Another novel already uses this slug/title variation. Please adjust the title.");
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

        private IActionResult RedirectToLocalOrDefault(string? returnUrl, Guid novelId)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id = novelId });
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

        private static bool IsSqlServerUniqueConstraintViolation(DbUpdateException ex, params string[] indexNames)
        {
            if (ex.InnerException is not SqlException sqlEx)
            {
                return false;
            }

            if (sqlEx.Number != 2601 && sqlEx.Number != 2627)
            {
                return false;
            }

            if (indexNames.Length == 0)
            {
                return true;
            }

            return indexNames.Any(indexName => sqlEx.Message.Contains(indexName, StringComparison.OrdinalIgnoreCase));
        }
    }
}