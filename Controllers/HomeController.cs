using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.ViewModels;
using System.Diagnostics;

namespace Reading_Writing_Platform.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _dbContext;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext dbContext)
        {
            _logger = logger;
            _dbContext = dbContext;
        }

        public async Task<IActionResult> Index()
        {
            string? currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var publishedNovelsQuery = _dbContext.Novels
                .Where(n => n.Status == NovelStatus.Published)
                .OrderByDescending(n => n.PublishedAt)
                .Select(n => new NovelListItemViewModel
                {
                    Id = n.Id,
                    Title = n.Title,
                    Slug = n.Slug,
                    Description = n.Description,
                    CoverImageUrl = n.CoverImageUrl,
                    Status = n.Status,
                    UpdatedAt = n.UpdatedAt,
                    ChapterCount = n.Chapters.Count,
                    LastReadChapterNumber = currentUserId != null
                        ? n.ReadingProgresses
                            .Where(rp => rp.UserId == currentUserId)
                            .Select(rp => (int?)rp.LastReadChapterNumber)
                            .FirstOrDefault()
                        : null
                });

            var vm = new HomeIndexViewModel
            {
                PublishedNovels = await publishedNovelsQuery.ToListAsync()
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
