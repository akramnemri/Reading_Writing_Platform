using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
using Reading_Writing_Platform.ViewModels;

namespace Reading_Writing_Platform.Areas.Author.Controllers
{
    [Area("Author")]
    [Authorize(Roles = RoleNames.Member + "," + RoleNames.Admin)]
    [Route("novels/{novelId:guid}/{novelSlug}/chapters")]
    public class ChapterController : Controller
    {
        private const int DefaultPage = 1;
        private const int MinPageSize = 5;
        private const int MaxPageSize = 50;

        private readonly ApplicationDbContext _dbContext;

        public ChapterController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("", Name = "AuthorChapterIndex")]
        public async Task<IActionResult> Index(
            Guid novelId,
            string novelSlug,
            string? q = null,
            string? status = null,
            int page = 1,
            int pageSize = 10)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            if (page < 1)
            {
                page = DefaultPage;
            }

            pageSize = Math.Clamp(pageSize, MinPageSize, MaxPageSize);

            IQueryable<Chapter> baseQuery = _dbContext.Chapters
                .Where(x => x.NovelId == novelId);

            // Single query to get all required counts with conditional aggregation
            var counts = await baseQuery
                .GroupBy(x => 1)
                .Select(g => new
                {
                    All = g.Count(),
                    Published = g.Count(x => x.Status == ChapterStatus.Published),
                    Draft = g.Count(x => x.Status == ChapterStatus.Draft),
                    Scheduled = g.Count(x => x.Status == ChapterStatus.Published && x.PublishedAt.HasValue && x.PublishedAt > DateTimeOffset.UtcNow)
                })
                .FirstOrDefaultAsync();

            int allCount = counts?.All ?? 0;
            int publishedCount = counts?.Published ?? 0;
            int draftCount = counts?.Draft ?? 0;
            int scheduledCount = counts?.Scheduled ?? 0;

            IQueryable<Chapter> query = baseQuery;

            if (!string.IsNullOrWhiteSpace(q))
            {
                string search = q.Trim();
                query = query.Where(x => x.Title.Contains(search));
            }

            string normalizedStatus = string.IsNullOrWhiteSpace(status)
                ? "all"
                : status.Trim().ToLowerInvariant();

            query = normalizedStatus switch
            {
                "published" => query.Where(x => x.Status == ChapterStatus.Published),
                "draft" => query.Where(x => x.Status == ChapterStatus.Draft),
                "scheduled" => query.Where(x =>
                    x.Status == ChapterStatus.Published &&
                    x.PublishedAt.HasValue &&
                    x.PublishedAt > DateTimeOffset.UtcNow),
                _ => query
            };

            int filteredCount = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize));
            if (page > totalPages)
            {
                page = totalPages;
            }

            var chapters = await query
                .OrderByDescending(x => x.ChapterNumber)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.Novel = novel;
            ViewBag.Query = q;
            ViewBag.Status = normalizedStatus;

            ViewBag.AllCount = allCount;
            ViewBag.PublishedCount = publishedCount;
            ViewBag.DraftCount = draftCount;
            ViewBag.ScheduledCount = scheduledCount;

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.FilteredCount = filteredCount;

            return View(chapters);
        }

        [HttpGet("create")]
        public async Task<IActionResult> Create(Guid novelId, string novelSlug)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            int nextOrder = await _dbContext.Chapters
                .Where(x => x.NovelId == novelId)
                .Select(x => (int?)x.Order)
                .MaxAsync() ?? 0;

            var allowedStatuses = novel.Status == NovelStatus.Published
                ? new List<SelectListItem>
                {
                    new SelectListItem { Text = ChapterStatus.Draft.ToString(), Value = ((int)ChapterStatus.Draft).ToString() },
                    new SelectListItem { Text = ChapterStatus.Published.ToString(), Value = ((int)ChapterStatus.Published).ToString() }
                }
                : new List<SelectListItem>
                {
                    new SelectListItem { Text = ChapterStatus.Draft.ToString(), Value = ((int)ChapterStatus.Draft).ToString() }
                };

            var vm = new ChapterFormViewModel
            {
                NovelId = novelId,
                NovelSlug = novel.Slug,
                Order = nextOrder + 1,
                NovelStatus = novel.Status,
                AvailableStatuses = allowedStatuses
            };

            return View(vm);
        }

        [HttpPost("create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Guid novelId, string novelSlug, ChapterFormViewModel vm)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            // Prevent modifications to completed novels
            if (novel.Status == NovelStatus.Completed)
            {
                ModelState.AddModelError(string.Empty, "Cannot add chapters to a completed novel.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }

            // Chapters can only be published if the novel itself is Published
            if (vm.Status == ChapterStatus.Published && novel.Status != NovelStatus.Published)
            {
                ModelState.AddModelError(nameof(vm.Status), "Chapters can only be published when the novel status is 'Published'.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                vm.NovelStatus = novel.Status;
                return View(vm);
            }

            if (!ModelState.IsValid)
            {
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                vm.NovelStatus = novel.Status;
                return View(vm);
            }

            var chapter = new Chapter
            {
                NovelId = novelId,
                Title = vm.Title.Trim(),
                Content = vm.Content,
                Status = vm.Status,
                IsLocked = vm.Status == ChapterStatus.Published ? vm.IsLocked : false,
                BasePrice = vm.BasePrice,
                Order = vm.Order < 1 ? 1 : vm.Order,
                ChapterNumber = 0,
                WordCount = CountWords(vm.Content),
                PublishedAt = vm.Status == ChapterStatus.Published ? DateTimeOffset.UtcNow : null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Chapters.Add(chapter);
            await _dbContext.SaveChangesAsync();
            await NormalizeChapterOrderingAsync(novelId);

            return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug = novel.Slug });
        }

        [HttpGet("{id:guid}/edit")]
        public async Task<IActionResult> Edit(Guid novelId, string novelSlug, Guid id)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            var chapter = await _dbContext.Chapters
                .FirstOrDefaultAsync(x => x.Id == id && x.NovelId == novelId);

            if (chapter is null)
            {
                return NotFound();
            }

            var allowedStatuses = novel.Status == NovelStatus.Published
                ? new List<SelectListItem>
                {
                    new SelectListItem { Text = ChapterStatus.Draft.ToString(), Value = ((int)ChapterStatus.Draft).ToString() },
                    new SelectListItem { Text = ChapterStatus.Published.ToString(), Value = ((int)ChapterStatus.Published).ToString() }
                }
                : new List<SelectListItem>
                {
                    new SelectListItem { Text = ChapterStatus.Draft.ToString(), Value = ((int)ChapterStatus.Draft).ToString() }
                };

            var vm = new ChapterFormViewModel
            {
                Id = chapter.Id,
                NovelId = chapter.NovelId,
                NovelSlug = novel.Slug,
                Title = chapter.Title,
                Content = chapter.Content,
                Status = chapter.Status,
                IsLocked = chapter.IsLocked,
                BasePrice = chapter.BasePrice,
                Order = chapter.Order,
                RowVersion = chapter.RowVersion,
                NovelStatus = novel.Status,
                AvailableStatuses = allowedStatuses
            };

            return View(vm);
        }

        [HttpPost("{id:guid}/edit")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid novelId, string novelSlug, Guid id, ChapterFormViewModel vm)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            // Prevent modifications to completed novels
            if (novel.Status == NovelStatus.Completed)
            {
                ModelState.AddModelError(string.Empty, "Cannot edit chapters for a completed novel.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }

            if (id != vm.Id)
            {
                return BadRequest();
            }

            var chapter = await _dbContext.Chapters
                .FirstOrDefaultAsync(x => x.Id == id && x.NovelId == novelId);

            if (chapter is null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }

            // Enforce publishing rule: chapter can only be published if novel is Published
            if (vm.Status == ChapterStatus.Published && chapter.Status != ChapterStatus.Published && novel.Status != NovelStatus.Published)
            {
                ModelState.AddModelError(nameof(vm.Status), "Chapters can only be published when the novel status is 'Published'.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                vm.NovelStatus = novel.Status;
                return View(vm);
            }

            if (!ModelState.IsValid)
            {
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                vm.NovelStatus = novel.Status;
                return View(vm);
            }

            bool wasPublished = chapter.Status == ChapterStatus.Published;

            chapter.Title = vm.Title.Trim();
            chapter.Content = vm.Content;
            chapter.Status = vm.Status;
            chapter.IsLocked = vm.Status == ChapterStatus.Published ? vm.IsLocked : false;
            chapter.BasePrice = vm.BasePrice;
            chapter.Order = vm.Order < 1 ? 1 : vm.Order;
            chapter.WordCount = CountWords(vm.Content);
            chapter.UpdatedAt = DateTimeOffset.UtcNow;

            if (!wasPublished && vm.Status == ChapterStatus.Published)
            {
                chapter.PublishedAt = DateTimeOffset.UtcNow;
            }

            if (wasPublished && vm.Status == ChapterStatus.Draft)
            {
                chapter.PublishedAt = null;
            }

            _dbContext.Entry(chapter).Property(x => x.RowVersion).OriginalValue = vm.RowVersion ?? [];

            try
            {
                await _dbContext.SaveChangesAsync();
                await NormalizeChapterOrderingAsync(novelId);
                return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug = novel.Slug });
            }
            catch (DbUpdateConcurrencyException)
            {
                ModelState.AddModelError(string.Empty, "This chapter was modified by another user. Reload and try again.");
                return View(vm);
            }
        }

        [HttpPost("publish")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Publish(Guid novelId, string novelSlug, Guid id)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            if (novel.Status != NovelStatus.Published)
            {
                TempData["ErrorMessage"] = "Novel must be published before chapters can be published.";
                return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug });
            }

            var chapter = await _dbContext.Chapters
                .FirstOrDefaultAsync(x => x.Id == id && x.NovelId == novelId);

            if (chapter is null)
            {
                return NotFound();
            }

            if (chapter.Status == ChapterStatus.Published)
            {
                TempData["InfoMessage"] = "Chapter is already published.";
                return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug });
            }

            chapter.Status = ChapterStatus.Published;
            chapter.PublishedAt = DateTimeOffset.UtcNow;
            chapter.UpdatedAt = DateTimeOffset.UtcNow;

            // Set IsLocked if not already set (only applicable for published chapters)
            if (chapter.IsLocked == false)
            {
                chapter.IsLocked = false; // default to unlocked
            }

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Chapter '{chapter.Title}' has been published.";
            return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug });
        }

        [HttpGet("{id:guid}/delete")]
        public async Task<IActionResult> Delete(Guid novelId, string novelSlug, Guid id)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            var chapter = await _dbContext.Chapters
                .Include(c => c.Novel)
                .FirstOrDefaultAsync(x => x.Id == id && x.NovelId == novelId);

            if (chapter is null)
            {
                return NotFound();
            }

            return View(chapter);
        }

        [HttpPost("{id:guid}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(Guid novelId, string novelSlug, Guid id)
        {
            var novel = await GetOwnedOrAdminNovelAsync(novelId);
            if (novel is null)
            {
                return NotFound();
            }

            var chapter = await _dbContext.Chapters
                .FirstOrDefaultAsync(x => x.Id == id && x.NovelId == novelId);

            if (chapter is null)
            {
                return NotFound();
            }

            _dbContext.Chapters.Remove(chapter);
            await _dbContext.SaveChangesAsync();
            await NormalizeChapterOrderingAsync(novelId);

            return RedirectToRoute("AuthorChapterIndex", new { novelId, novelSlug = novel.Slug });
        }

        private async Task<Novel?> GetOwnedOrAdminNovelAsync(Guid novelId)
        {
            IQueryable<Novel> query = _dbContext.Novels;
            if (!User.IsInRole(RoleNames.Admin))
            {
                string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                query = query.Where(x => x.AuthorUserId == userId);
            }
            return await query.FirstOrDefaultAsync(x => x.Id == novelId);
        }

        private async Task NormalizeChapterOrderingAsync(Guid novelId)
        {
            var chapters = await _dbContext.Chapters
                .Where(x => x.NovelId == novelId)
                .OrderBy(x => x.Order)
                .ThenBy(x => x.CreatedAt)
                .ToListAsync();

            for (int i = 0; i < chapters.Count; i++)
            {
                chapters[i].Order = i + 1;
                chapters[i].ChapterNumber = i + 1;
            }

            await _dbContext.SaveChangesAsync();
        }

        private static int CountWords(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return 0;
            }

            return content
                .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }
    }
}