using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SubiteAPI.Data;
using SubiteAPI.Models;
using SubiteAPI.Options;

namespace SubiteAPI.Infrastructure;

public static class AdminDataSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IOptions<AdminOptions> adminOptions)
    {
        if (!await roleManager.RoleExistsAsync(AppRoles.Admin).ConfigureAwait(false))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(AppRoles.Admin)).ConfigureAwait(false);
        }

        var opts = adminOptions.Value;
        var email = opts.Email.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var admin = await userManager.FindByEmailAsync(email).ConfigureAwait(false);
        if (admin == null)
        {
            admin = new User
            {
                Email = email,
                UserName = email,
                FullName = string.IsNullOrWhiteSpace(opts.FullName) ? "Administrador" : opts.FullName,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, opts.Password).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"No se pudo crear el admin seed: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AppRoles.Admin).ConfigureAwait(false))
        {
            await userManager.AddToRoleAsync(admin, AppRoles.Admin).ConfigureAwait(false);
        }

        if (!await db.PlatformSettings.AnyAsync().ConfigureAwait(false))
        {
            db.PlatformSettings.Add(new PlatformSettings
            {
                Id = 1,
                PlatformCommissionRate = 0.125m,
                UpdatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync().ConfigureAwait(false);
        }
    }
}
