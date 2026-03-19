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

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                return;
            }

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
        }
    }
}