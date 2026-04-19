using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;
using Reading_Writing_Platform.Security;

namespace Reading_Writing_Platform.Data;

public static class NovelSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        // Seed themes if empty
        if (!await db.Themes.AnyAsync())
        {
            var themes = new[]
            {
                "Fantasy", "Adventure", "Romance", "Mystery", "Sci-Fi", "Horror", "Thriller", "Comedy", "Drama", "Action"
            };
            foreach (var t in themes)
            {
                db.Themes.Add(new Theme { Name = t });
            }
            await db.SaveChangesAsync();
        }

        // Seed author user if not exists
        const string authorEmail = "author@seed.com";
        var author = await userManager.FindByEmailAsync(authorEmail);
        if (author == null)
        {
            author = new IdentityUser
            {
                UserName = authorEmail,
                Email = authorEmail,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(author, "SeedPass123!");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(author, RoleNames.Member);
                await userManager.AddToRoleAsync(author, RoleNames.Author);
            }
        }

        if (author == null) return;

        // Ensure user profile exists
        var profile = await db.UserProfiles.FirstOrDefaultAsync(p => p.UserId == author.Id);
        if (profile == null)
        {
            db.UserProfiles.Add(new UserProfile
            {
                UserId = author.Id,
                DisplayName = "Seed Author",
                Intent = ProfileIntent.Both
            });
            await db.SaveChangesAsync();
        }

        // If novels already exist, skip
        if (await db.Novels.AnyAsync()) return;

        var random = new Random(0);
        var statuses = new[]
        {
            NovelStatus.Draft, NovelStatus.Submitted, NovelStatus.Approved, NovelStatus.Rejected,
            NovelStatus.Published, NovelStatus.Paused, NovelStatus.Dropped, NovelStatus.Completed
        };

        string[] words = { "Journey", "Adventure", "Mystery", "Tale", "Story", "Chronicle", "Legend", "Saga" };

        for (int i = 1; i <= 12; i++)
        {
            var status = statuses[(i - 1) % statuses.Length];
            string word = words[random.Next(words.Length)];
            var title = $"Novel {i}: The {word}";
            var slug = $"novel-{i}-{word.ToLower()}";

            var novel = new Novel
            {
                AuthorUserId = author.Id,
                Title = title,
                Slug = slug,
                Description = $"This is a sample novel (#{i}) with an intriguing story. It explores themes of adventure and discovery.",
                CoverImageUrl = $"https://picsum.photos/seed/{i}/200/300",
                Status = status,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-30 * i),
                UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30 * i),
                SubmissionCount = status == NovelStatus.Rejected ? 2 : (status == NovelStatus.Submitted ? 1 : 0),
                SubmittedForReviewAt = (status == NovelStatus.Submitted || status == NovelStatus.Rejected || status == NovelStatus.Approved)
                    ? DateTimeOffset.UtcNow.AddDays(-30 * i + 5)
                    : null,
                ApprovedAt = status == NovelStatus.Approved ? DateTimeOffset.UtcNow.AddDays(-30 * i + 3) : null,
                RejectedAt = status == NovelStatus.Rejected ? DateTimeOffset.UtcNow.AddDays(-30 * i + 2) : null,
                RejectionReason = status == NovelStatus.Rejected ? "The plot needs more development. Please expand the worldbuilding." : null,
                PublishedAt = (status == NovelStatus.Published || status == NovelStatus.Paused || status == NovelStatus.Dropped || status == NovelStatus.Completed)
                    ? DateTimeOffset.UtcNow.AddDays(-30 * i + 10)
                    : null
            };

            // Assign 1-3 random themes
            var themeCount = random.Next(1, 4);
            var allThemeIds = await db.Themes.Select(t => t.Id).ToListAsync();
            var selectedThemeIds = allThemeIds.OrderBy(_ => random.Next()).Take(themeCount).ToList();
            foreach (var tid in selectedThemeIds)
            {
                novel.NovelThemes.Add(new NovelTheme { ThemeId = tid });
            }

            db.Novels.Add(novel);
            await db.SaveChangesAsync();

            // Add chapters
            int chapterCount = random.Next(3, 8);
            for (int c = 1; c <= chapterCount; c++)
            {
                ChapterStatus chapterStatus = ChapterStatus.Published;
                if (status == NovelStatus.Draft || status == NovelStatus.Rejected)
                {
                    chapterStatus = ChapterStatus.Draft;
                }
                else if (status == NovelStatus.Submitted)
                {
                    chapterStatus = ChapterStatus.Draft;
                }
                else if (status == NovelStatus.Approved)
                {
                    chapterStatus = c <= 2 ? ChapterStatus.Published : ChapterStatus.Draft;
                }
                else if (status == NovelStatus.Paused || status == NovelStatus.Dropped || status == NovelStatus.Completed)
                {
                    chapterStatus = ChapterStatus.Published;
                }

                var contentBuilder = "";
                for (int p = 0; p < 20; p++)
                {
                    contentBuilder += $"<p>This is part {p + 1} of chapter {c}. It contains many words to tell the story.</p>";
                }

                var chapter = new Chapter
                {
                    NovelId = novel.Id,
                    Title = $"Chapter {c}: The {(c % 2 == 0 ? "Twist" : "Journey")}",
                    Content = contentBuilder,
                    Status = chapterStatus,
                    IsLocked = random.NextDouble() > 0.7,
                    BasePrice = chapterStatus == ChapterStatus.Published && random.NextDouble() > 0.8 ? 5 : 0,
                    Order = c,
                    ChapterNumber = c,
                    WordCount = random.Next(500, 2500),
                    PublishedAt = chapterStatus == ChapterStatus.Published ? DateTimeOffset.UtcNow.AddDays(-30 * i + 10 + c) : null,
                    CreatedAt = DateTimeOffset.UtcNow.AddDays(-30 * i),
                    UpdatedAt = DateTimeOffset.UtcNow.AddDays(-30 * i)
                };
                db.Chapters.Add(chapter);
            }
            await db.SaveChangesAsync();
        }
    }
}
