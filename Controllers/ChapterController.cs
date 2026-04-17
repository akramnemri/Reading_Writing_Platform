using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
using Reading_Writing_Platform.ViewModels;

namespace Reading_Writing_Platform.Controllers
{
    [Authorize(Roles = $"{RoleNames.Author},{RoleNames.Admin}")]
    [Route("novels/{novelId:guid}/{novelSlug}/chapters")]
    public class ChapterController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public ChapterController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpGet("")]
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
                page = 1;
            }

            pageSize = Math.Clamp(pageSize, 5, 50);

            IQueryable<Chapter> baseQuery = _dbContext.Chapters
                .Where(x => x.NovelId == novelId);

            int allCount = await baseQuery.CountAsync();
            int publishedCount = await baseQuery.CountAsync(x => x.Status == ChapterStatus.Published);
            int draftCount = await baseQuery.CountAsync(x => x.Status == ChapterStatus.Draft);
            int scheduledCount = await baseQuery.CountAsync(x =>
                x.Status == ChapterStatus.Published &&
                x.PublishedAt.HasValue &&
                x.PublishedAt > DateTimeOffset.UtcNow);

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

            // Ajouter ces URLs pré-construites
            string baseUrl = $"/novels/{novelId}/{novelSlug}/chapters";
            ViewBag.CreateUrl = baseUrl + "/create";
            ViewBag.FilterAllUrl = BuildFilterUrl(baseUrl, "all", q, pageSize);
            ViewBag.FilterPublishedUrl = BuildFilterUrl(baseUrl, "published", q, pageSize);
            ViewBag.FilterScheduledUrl = BuildFilterUrl(baseUrl, "scheduled", q, pageSize);
            ViewBag.FilterDraftUrl = BuildFilterUrl(baseUrl, "draft", q, pageSize);

            return View(chapters);
        }

        private string BuildFilterUrl(string baseUrl, string status, string? q, int pageSize)
        {
            var query = new List<string>();
            
            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                query.Add($"status={status}");
            }
            
            if (!string.IsNullOrWhiteSpace(q))
            {
                query.Add($"q={Uri.EscapeDataString(q)}");
            }
            
            query.Add("page=1");
            query.Add($"pageSize={pageSize}");
            
            return baseUrl + (query.Count > 0 ? "?" + string.Join("&", query) : "");
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

            var vm = new ChapterFormViewModel
            {
                NovelId = novelId,
                NovelSlug = novel.Slug,
                Order = nextOrder + 1
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

            if (!ModelState.IsValid)
            {
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }

            var chapter = new Chapter
            {
                NovelId = novelId,
                Title = vm.Title.Trim(),
                Content = vm.Content,
                Status = vm.Status,
                IsLocked = vm.IsLocked,
                BasePrice = vm.BasePrice,
                PreviewCharCount = vm.PreviewCharCount,
                Order = vm.Order < 1 ? 1 : vm.Order,
                ChapterNumber = 0,
                WordCount = CountWords(vm.Content),
                PublishedAt = vm.Status == ChapterStatus.Published ? DateTimeOffset.UtcNow : null,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                _dbContext.Chapters.Add(chapter);
                await _dbContext.SaveChangesAsync();
                await NormalizeChapterOrderingAsync(novelId);
                return RedirectToAction(nameof(Index), new { novelId, novelSlug = novel.Slug });
            }
            catch (DbUpdateException ex) when (IsSqlServerUniqueConstraintViolation(ex, "IX_Chapters_NovelId_ChapterNumber", "IX_Chapters_NovelId_Order"))
            {
                ModelState.AddModelError(nameof(vm.Order), "Another chapter already uses this number/order. Change the position and retry.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }
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
                PreviewCharCount = chapter.PreviewCharCount,
                Order = chapter.Order,
                RowVersion = chapter.RowVersion
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

            bool wasPublished = chapter.Status == ChapterStatus.Published;

            chapter.Title = vm.Title.Trim();
            chapter.Content = vm.Content;
            chapter.Status = vm.Status;
            chapter.IsLocked = vm.IsLocked;
            chapter.BasePrice = vm.BasePrice;
            chapter.PreviewCharCount = vm.PreviewCharCount;
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
                return RedirectToAction(nameof(Index), new { novelId, novelSlug = novel.Slug });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var entry = ex.Entries.SingleOrDefault();
                if (entry?.Entity is Chapter)
                {
                    var databaseValues = await entry.GetDatabaseValuesAsync();
                    if (databaseValues is null)
                    {
                        ModelState.AddModelError(string.Empty, "This chapter was deleted by another user.");
                    }
                    else
                    {
                        var databaseChapter = (Chapter)databaseValues.ToObject();
                        vm.RowVersion = databaseChapter.RowVersion;
                        ModelState.AddModelError(string.Empty, "This chapter was modified by another user. Reload and apply your changes again.");
                    }
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "A concurrency error occurred. Reload and try again.");
                }

                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }
            catch (DbUpdateException ex) when (IsSqlServerUniqueConstraintViolation(ex, "IX_Chapters_NovelId_ChapterNumber", "IX_Chapters_NovelId_Order"))
            {
                ModelState.AddModelError(nameof(vm.Order), "Another chapter already uses this number/order. Change the position and retry.");
                vm.NovelId = novelId;
                vm.NovelSlug = novel.Slug;
                return View(vm);
            }
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

            return RedirectToAction(nameof(Index), new { novelId, novelSlug = novel.Slug });
        }

        private async Task<Novel?> GetOwnedOrAdminNovelAsync(Guid novelId)
        {
            if (User.IsInRole(RoleNames.Admin))
            {
                return await _dbContext.Novels.FirstOrDefaultAsync(x => x.Id == novelId);
            }

            string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _dbContext.Novels
                .FirstOrDefaultAsync(x => x.Id == novelId && x.AuthorUserId == userId);
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