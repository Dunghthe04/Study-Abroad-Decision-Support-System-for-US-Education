using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace StudyAbroad.Api.Extensions;

public static class HealthCheckExtensions
{
    /// <summary>/health/live: process is up. /health/ready: dependencies (database) are reachable.</summary>
    public static void MapAppHealthChecks(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
    }
}
