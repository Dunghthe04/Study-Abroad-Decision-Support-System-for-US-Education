using Microsoft.EntityFrameworkCore;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>Applies EF Core migrations at startup when Database:MigrateOnStartup is true; seeds demo data in Development.</summary>
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup")) return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (app.Environment.IsDevelopment())
            await DbSeeder.SeedAsync(db);
    }
}
