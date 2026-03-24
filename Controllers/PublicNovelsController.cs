using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;

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
            var novel = await _dbContext.Novels
                .Include(x => x.AuthorUser)
                .Include(x => x.NovelThemes)
                    .ThenInclude(x => x.Theme)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == NovelStatus.Published);

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

            return View(novel);
        }

        // GET /novels/{slug}/chapters/{chapterNumber}
        [HttpGet("{slug}/chapters/{chapterNumber:int}")]
        public async Task<IActionResult> ReadChapter(string slug, int chapterNumber)
        {
            var novel = await _dbContext.Novels
                .Include(x => x.AuthorUser)
                .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == NovelStatus.Published);

            if (novel is null)
                return NotFound();

            var chapter = await _dbContext.Chapters
                .FirstOrDefaultAsync(x =>
                    x.NovelId == novel.Id &&
                    x.ChapterNumber == chapterNumber &&
                    x.Status == ChapterStatus.Published);

            if (chapter is null)
                return NotFound();

            var publishedChapters = await _dbContext.Chapters
                .Where(x => x.NovelId == novel.Id && x.Status == ChapterStatus.Published)
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