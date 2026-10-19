using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetMvcAuthV2.Common.Enums;
using NetMvcAuthV2.Mappings;
using NetMvcAuthV2.Models;

namespace NetMvcAuthV2.Data.Seed;

public static class DbSeeder
{
    // Idempotent: aman dipanggil tiap startup. Kredensial admin dari config (user-secrets), bukan hardcode.
    public static async Task SeedAdminAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DbSeeder));

        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Seed admin dilewati: Seed:AdminEmail / Seed:AdminPassword belum diisi.");
            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var normalizedEmail = UserMappings.NormalizeEmail(email);

        if (await context.Users.AnyAsync(u => u.Email == normalizedEmail))
            return;

        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var admin = new User
        {
            Email = normalizedEmail,
            FullName = config["Seed:AdminName"] ?? "Administrator",
            Role = UserRole.Admin
        };
        admin.PasswordHash = hasher.HashPassword(admin, password);

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        logger.LogInformation("Akun admin di-seed: {Email}", normalizedEmail);
    }
}
