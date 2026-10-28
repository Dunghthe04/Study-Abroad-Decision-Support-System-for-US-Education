using Microsoft.EntityFrameworkCore;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>Áp EF Core migrations khi khởi động nếu Database:MigrateOnStartup = true; seed demo ở Development.</summary>
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup")) return;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        if (app.Environment.IsDevelopment())
            await DbSeeder.SeedAsync(db, app.Configuration["SEED_ADMIN_PASSWORD"]);
    }
}
