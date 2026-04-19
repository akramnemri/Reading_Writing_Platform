using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;
namespace Reading_Writing_Platform.Controllers
{
    [Route("novels")]
    public class PublicNovelsController : Controller
    {
        private readonly ApplicationDbContext _dbContext;

        public PublicNovelsController(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // GET /novels/{slug}
        [HttpGet("{slug}")]
        public async Task<IActionResult> Details(string slug)
        {
            bool isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Admin);

            var isPublic = new[] { NovelStatus.Published, NovelStatus.Paused, NovelStatus.Dropped, NovelStatus.Completed };

            var novelQuery = _dbContext.Novels
                .Include(x => x.AuthorUser)
                .Include(x => x.NovelThemes)
                    .ThenInclude(x => x.Theme)
                .Where(x => x.Slug == slug);

            // Non-admins only see public novels
            if (!isAdmin)
            {
                novelQuery = novelQuery.Where(x => isPublic.Contains(x.Status));
            }

            var novel = await novelQuery.FirstOrDefaultAsync();

            if (novel is null)
                return NotFound();

            novel.Chapters = await _dbContext.Chapters
                .Where(x => x.NovelId == novel.Id && x.Status == ChapterStatus.Published)
                .OrderBy(x => x.ChapterNumber)
                .Select(x => new Chapter
                {
                    Id = x.Id,
                    ChapterNumber = x.ChapterNumber,
                    Title = x.Title,
                    WordCount = x.WordCount,
                    PublishedAt = x.PublishedAt,
                    Status = x.Status,
                    NovelId = x.NovelId
                })
                .ToListAsync();

            // Get reading progress for current user
            if (User.Identity?.IsAuthenticated == true)
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.IsNullOrEmpty(userId))
                {
                    var progress = await _dbContext.ReadingProgresses
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.NovelId == novel.Id);
                    if (progress != null)
                    {
                        ViewBag.LastReadChapterNumber = progress.LastReadChapterNumber;
                    }
                }
            }

            return View(novel);
        }

        // GET /novels/{slug}/chapters/{chapterNumber:int}
        [HttpGet("{slug}/chapters/{chapterNumber:int}")]
        public async Task<IActionResult> ReadChapter(string slug, int chapterNumber)
        {
            // Allow admins to preview chapters even if the novel is not yet published
            bool isAdmin = User.Identity?.IsAuthenticated == true && User.IsInRole(RoleNames.Admin);

            var isPublic = new[] { NovelStatus.Published, NovelStatus.Paused, NovelStatus.Dropped, NovelStatus.Completed };

            var novelQuery = _dbContext.Novels
                .Include(x => x.AuthorUser)
                .Where(x => x.Slug == slug);

            // Admins can see any status; public users see only publicly visible statuses
            if (!isAdmin)
            {
                novelQuery = novelQuery.Where(x => isPublic.Contains(x.Status));
            }

            var novel = await novelQuery.FirstOrDefaultAsync();

            if (novel is null)
                return NotFound();

            // For chapter: Admins see any chapter, normal users see only Published
            var chapterQuery = _dbContext.Chapters
                .Where(x => x.NovelId == novel.Id && x.ChapterNumber == chapterNumber);

            if (!isAdmin)
            {
                chapterQuery = chapterQuery.Where(x => x.Status == ChapterStatus.Published);
            }

            var chapter = await chapterQuery.FirstOrDefaultAsync();

            if (chapter is null)
                return NotFound();

            // Save reading progress for authenticated users
            if (User.Identity?.IsAuthenticated == true)
            {
                string userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                if (!string.IsNullOrEmpty(userId))
                {
                    var progress = await _dbContext.ReadingProgresses
                        .FirstOrDefaultAsync(x => x.UserId == userId && x.NovelId == novel.Id);

                    if (progress is null)
                    {
                        progress = new ReadingProgress
                        {
                            UserId = userId,
                            NovelId = novel.Id,
                            LastReadChapterNumber = chapterNumber,
                            LastReadAt = DateTimeOffset.UtcNow,
                            CreatedAt = DateTimeOffset.UtcNow
                        };
                        _dbContext.ReadingProgresses.Add(progress);
                    }
                    else
                    {
                        progress.LastReadChapterNumber = chapterNumber;
                        progress.LastReadAt = DateTimeOffset.UtcNow;
                        progress.UpdatedAt = DateTimeOffset.UtcNow;
                    }

                    await _dbContext.SaveChangesAsync();
                }
            }

            // Previous / Next logic - only consider visible chapters for the current user
            var visibleChaptersQuery = _dbContext.Chapters
                .Where(x => x.NovelId == novel.Id);

            if (!isAdmin)
            {
                visibleChaptersQuery = visibleChaptersQuery.Where(x => x.Status == ChapterStatus.Published);
            }

            var publishedChapters = await visibleChaptersQuery
                .OrderBy(x => x.ChapterNumber)
                .Select(x => x.ChapterNumber)
                .ToListAsync();

            int currentIndex = publishedChapters.IndexOf(chapterNumber);

            ViewBag.Novel = novel;
            ViewBag.PrevChapterNumber = currentIndex > 0 ? publishedChapters[currentIndex - 1] : (int?)null;
            ViewBag.NextChapterNumber = currentIndex < publishedChapters.Count - 1 ? publishedChapters[currentIndex + 1] : (int?)null;

            return View(chapter);
        }
    }
}