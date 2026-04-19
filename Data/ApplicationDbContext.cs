using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Novel> Novels => Set<Novel>();
        public DbSet<Chapter> Chapters => Set<Chapter>();
        public DbSet<Theme> Themes => Set<Theme>();
        public DbSet<NovelTheme> NovelThemes => Set<NovelTheme>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<ReadingProgress> ReadingProgresses => Set<ReadingProgress>();
        public DbSet<ReviewHistory> ReviewHistories => Set<ReviewHistory>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Novel>(entity =>
            {
                entity.Property(x => x.Slug)
                    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

                entity.HasIndex(x => x.Slug).IsUnique();
                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasIndex(x => x.AuthorUserId);
                entity.HasIndex(x => x.ReviewedByUserId);
                entity.HasIndex(x => new { x.Status, x.SubmittedForReviewAt });

                entity.HasOne(x => x.AuthorUser)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.ReviewedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.ReviewedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Chapter>(entity =>
            {
                entity.HasIndex(x => new { x.NovelId, x.ChapterNumber }).IsUnique();
                entity.HasIndex(x => new { x.NovelId, x.Order }).IsUnique();
                entity.HasIndex(x => new { x.NovelId, x.Status, x.PublishedAt });

                entity.Property(x => x.BasePrice).HasPrecision(10, 2);
                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasOne(x => x.Novel)
                    .WithMany(x => x.Chapters)
                    .HasForeignKey(x => x.NovelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Theme>(entity =>
            {
                entity.Property(x => x.Name)
                    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

                entity.HasIndex(x => x.Name).IsUnique();
            });

            builder.Entity<NovelTheme>(entity =>
            {
                entity.HasKey(x => new { x.NovelId, x.ThemeId });

                entity.HasOne(x => x.Novel)
                    .WithMany(x => x.NovelThemes)
                    .HasForeignKey(x => x.NovelId);

                entity.HasOne(x => x.Theme)
                    .WithMany(x => x.NovelThemes)
                    .HasForeignKey(x => x.ThemeId);
            });

            builder.Entity<UserProfile>(entity =>
            {
                entity.HasIndex(x => x.UserId).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ReadingProgress>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => new { x.UserId, x.NovelId }).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Novel)
                    .WithMany(n => n.ReadingProgresses)
                    .HasForeignKey(x => x.NovelId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<ReviewHistory>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.HasIndex(x => x.NovelId);
                entity.HasIndex(x => x.PerformedByUserId);
                entity.HasIndex(x => x.PerformedAt);

                entity.HasOne(x => x.Novel)
                    .WithMany()
                    .HasForeignKey(x => x.NovelId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.PerformedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.PerformedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
