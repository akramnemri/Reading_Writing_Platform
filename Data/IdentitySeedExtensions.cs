using Microsoft.AspNetCore.Identity;
using Reading_Writing_Platform.Security;

namespace Reading_Writing_Platform.Data
{
    public static class IdentitySeedExtensions
    {
        public static async Task SeedIdentityDataAsync(this IServiceProvider services, IConfiguration configuration)
        {
            using var scope = services.CreateScope();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            string[] roles = [RoleNames.Author, RoleNames.Member, RoleNames.Admin];

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                    {
                        var errors = string.Join("; ", roleResult.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Unable to create role '{role}': {errors}");
                    }
                }
            }

            var adminEmail = configuration["IdentitySeed:AdminEmail"];
            var adminPassword = configuration["IdentitySeed:AdminPassword"];

            if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
            {
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser is null)
                {
                    adminUser = new IdentityUser
                    {
                        UserName = adminEmail,
                        Email = adminEmail,
                        EmailConfirmed = true
                    };

                    var userResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (!userResult.Succeeded)
                    {
                        var errors = string.Join("; ", userResult.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Unable to create admin user: {errors}");
                    }
                }

            if (!await userManager.IsInRoleAsync(adminUser, RoleNames.Admin))
            {
                var addToRoleResult = await userManager.AddToRoleAsync(adminUser, RoleNames.Admin);
                if (!addToRoleResult.Succeeded)
                {
                    var errors = string.Join("; ", addToRoleResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"Unable to assign Admin role: {errors}");
                }
            }

            // Do NOT assign Member role to admin; admin is separate. If admin needs member privileges (e.g., commenting), assign manually.
            }

            // Backfill: any user without role becomes Member
            var users = userManager.Users.ToList();
            foreach (var user in users)
            {
                var rolesForUser = await userManager.GetRolesAsync(user);
                if (rolesForUser.Count == 0)
                {
                    var addMemberResult = await userManager.AddToRoleAsync(user, RoleNames.Member);
                    if (!addMemberResult.Succeeded)
                    {
                        var errors = string.Join("; ", addMemberResult.Errors.Select(e => e.Description));
                        throw new InvalidOperationException($"Unable to assign default role '{RoleNames.Member}' to user '{user.Email ?? user.Id}': {errors}");
                    }
                }
            }
        }
    }
}