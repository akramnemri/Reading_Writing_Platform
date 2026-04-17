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
        public DbSet<ChapterEntitlement> ChapterEntitlements => Set<ChapterEntitlement>();
        public DbSet<UserWallet> UserWallets => Set<UserWallet>();
        public DbSet<CoinTransaction> CoinTransactions => Set<CoinTransaction>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Novel>(entity =>
            {
                entity.Property(x => x.Slug)
                    .UseCollation("SQL_Latin1_General_CP1_CI_AS");

                entity.HasIndex(x => x.Slug).IsUnique();
                entity.Property(x => x.RowVersion).IsRowVersion();

                entity.HasOne(x => x.AuthorUser)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<Chapter>(entity =>
            {
                entity.HasIndex(x => new { x.NovelId, x.ChapterNumber }).IsUnique();
                entity.HasIndex(x => new { x.NovelId, x.Order }).IsUnique();

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

            builder.Entity<ChapterEntitlement>(entity =>
            {
                entity.HasIndex(x => new { x.ChapterId, x.UserId });
                entity.HasIndex(x => x.UserId);

                entity.HasOne(x => x.Chapter)
                    .WithMany()
                    .HasForeignKey(x => x.ChapterId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Transaction)
                    .WithMany()
                    .HasForeignKey(x => x.TransactionId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            builder.Entity<UserWallet>(entity =>
            {
                entity.HasIndex(x => x.UserId).IsUnique();

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<CoinTransaction>(entity =>
            {
                entity.HasIndex(x => x.UserId);
                entity.HasIndex(x => x.ChapterId);
                entity.HasIndex(x => x.CreatedAt);

                entity.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Chapter)
                    .WithMany()
                    .HasForeignKey(x => x.ChapterId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}
