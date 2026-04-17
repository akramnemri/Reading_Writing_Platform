using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reading_Writing_Platform.Data;
using Reading_Writing_Platform.Models;

namespace Reading_Writing_Platform.Services
{
    public class ChapterAccessRequirement : IAuthorizationRequirement
    {
    }

    public class ChapterAccessAuthorizationHandler : AuthorizationHandler<ChapterAccessRequirement, Chapter>
    {
        private readonly IServiceProvider _serviceProvider;

        public ChapterAccessAuthorizationHandler(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ChapterAccessRequirement requirement,
            Chapter resource)
        {
            // Chapitre non verrouillé : tout le monde peut lire
            if (!resource.IsLocked)
            {
                context.Succeed(requirement);
                return;
            }

            string? userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Pas connecté : refus
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            // Créer un scope pour résoudre le DbContext
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Auteur du novel : accès total
            var novel = await dbContext.Novels
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == resource.NovelId);

            if (novel is not null && novel.AuthorUserId == userId)
            {
                context.Succeed(requirement);
                return;
            }

            // Vérifier si l'utilisateur a un entitlement
            bool hasEntitlement = await dbContext.ChapterEntitlements
                .AnyAsync(e => e.ChapterId == resource.Id && e.UserId == userId);

            if (hasEntitlement)
            {
                context.Succeed(requirement);
            }
        }
    }
}