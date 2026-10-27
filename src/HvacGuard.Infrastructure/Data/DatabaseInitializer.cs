using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HvacGuard.Infrastructure.Data;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies EF Core migrations (or creates the schema when no migrations exist yet) and seeds demo data.
    /// Retries because SQL Server in Docker may still be starting when the web app boots.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(DatabaseInitializer));
        var db = provider.GetRequiredService<ApplicationDbContext>();

        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (db.Database.GetMigrations().Any())
                {
                    await db.Database.MigrateAsync(ct);
                }
                else
                {
                    await db.Database.EnsureCreatedAsync(ct);
                }

                break;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                logger.LogWarning(ex, "Database not ready (attempt {Attempt}/{MaxAttempts}), retrying in 5 seconds…",
                    attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }

        await DbSeeder.SeedAsync(provider, ct);
        logger.LogInformation("Database ready.");
    }
}
