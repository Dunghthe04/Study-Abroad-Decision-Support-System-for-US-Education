using Microsoft.EntityFrameworkCore;
using StudyAbroad.Infrastructure.Persistence;
using StudyAbroad.Infrastructure.Persistence.Migrations;

namespace StudyAbroad.Api.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies database migrations at startup when Database:MigrateOnStartup is true.
    /// Supports both EF Core migrations and Versioned SQL Migrations (SqlMigrationRunner).
    /// </summary>
    public static async Task ApplyDatabaseMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:MigrateOnStartup")) return;

        using var scope = app.Services.CreateScope();

        if (app.Configuration.GetValue<bool>("Database:UseSqlMigrations"))
        {
            await app.Services.RunSqlMigrationsAsync();
        }
        else
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
        }

        if (app.Environment.IsDevelopment())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DbSeeder.SeedAsync(db, app.Configuration["SEED_ADMIN_PASSWORD"]);
        }
    }

    /// <summary>
    /// Explicitly runs versioned SQL migrations (V1, V2...) via SqlMigrationRunner.
    /// </summary>
    public static async Task ApplySqlMigrationsAsync(this WebApplication app)
    {
        await app.Services.RunSqlMigrationsAsync();
    }
}

