using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-110 / UC-68] Mốc tính hạn 7 ngày và 90 ngày là CreatedAt + PausedSeconds, phải dịch được sang SQL.</summary>
public class LeadActiveSinceSqlTests
{
    [Fact]
    public void ActiveSince_TranslatesToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var cutoff = DateTime.UtcNow;

        var sql = db.Leads.Where(l => l.Status == LeadStatuses.New && l.CreatedAt.AddSeconds(l.PausedSeconds) <= cutoff).ToQueryString();

        Assert.Contains("paused_seconds", sql);
        Assert.Contains("created_at", sql);
    }
}
