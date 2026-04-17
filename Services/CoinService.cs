using Microsoft.EntityFrameworkCore;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.Services
{
    public interface ICoinService
    {
        Task<UserWallet> GetOrCreateWalletAsync(string userId);
        Task<bool> HasSufficientBalanceAsync(string userId, int amount);
        Task<bool> UnlockChapterAsync(string userId, Guid chapterId);
        Task AddCoinsAsync(string userId, int amount, string? description = null, string? externalTransactionId = null);
    }

    public class CoinService : ICoinService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<CoinService> _logger;

        public CoinService(ApplicationDbContext dbContext, ILogger<CoinService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task<UserWallet> GetOrCreateWalletAsync(string userId)
        {
            var wallet = await _dbContext.UserWallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet is null)
            {
                wallet = new UserWallet
                {
                    UserId = userId,
                    CoinsBalance = 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.UserWallets.Add(wallet);
                await _dbContext.SaveChangesAsync();
            }

            return wallet;
        }

        public async Task<bool> HasSufficientBalanceAsync(string userId, int amount)
        {
            var wallet = await GetOrCreateWalletAsync(userId);
            return wallet.CoinsBalance >= amount;
        }

        public async Task<bool> UnlockChapterAsync(string userId, Guid chapterId)
        {
            var chapter = await _dbContext.Chapters.FirstOrDefaultAsync(c => c.Id == chapterId);
            if (chapter is null || !chapter.IsLocked)
            {
                return false;
            }

            var existingEntitlement = await _dbContext.ChapterEntitlements
                .AnyAsync(e => e.ChapterId == chapterId && e.UserId == userId);

            if (existingEntitlement)
            {
                return true;
            }

            var wallet = await GetOrCreateWalletAsync(userId);

            if (wallet.CoinsBalance < chapter.BasePrice)
            {
                return false;
            }

            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                wallet.CoinsBalance -= chapter.BasePrice;
                wallet.UpdatedAt = DateTimeOffset.UtcNow;

                var coinTransaction = new CoinTransaction
                {
                    UserId = userId,
                    Type = CoinTransactionType.ChapterUnlock,
                    Amount = -chapter.BasePrice,
                    BalanceAfter = wallet.CoinsBalance,
                    Description = $"Unlocked: {chapter.Title}",
                    ChapterId = chapterId,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _dbContext.CoinTransactions.Add(coinTransaction);
                await _dbContext.SaveChangesAsync();

                var entitlement = new ChapterEntitlement
                {
                    ChapterId = chapterId,
                    UserId = userId,
                    CoinsCost = chapter.BasePrice,
                    TransactionId = coinTransaction.Id,
                    GrantedAt = DateTimeOffset.UtcNow
                };

                _dbContext.ChapterEntitlements.Add(entitlement);
                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                _logger.LogInformation("User {UserId} unlocked chapter {ChapterId} for {Coins} coins", userId, chapterId, chapter.BasePrice);

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to unlock chapter {ChapterId} for user {UserId}", chapterId, userId);
                return false;
            }
        }

        public async Task AddCoinsAsync(string userId, int amount, string? description = null, string? externalTransactionId = null)
        {
            var wallet = await GetOrCreateWalletAsync(userId);

            wallet.CoinsBalance += amount;
            wallet.UpdatedAt = DateTimeOffset.UtcNow;

            var transaction = new CoinTransaction
            {
                UserId = userId,
                Type = CoinTransactionType.Purchase,
                Amount = amount,
                BalanceAfter = wallet.CoinsBalance,
                Description = description ?? "Coins purchase",
                ExternalTransactionId = externalTransactionId,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.CoinTransactions.Add(transaction);
            await _dbContext.SaveChangesAsync();
        }
    }
}