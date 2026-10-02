using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Infrastructure.Advisor;
using StudyAbroad.Infrastructure.Persistence;
using StudyAbroad.Infrastructure.Persistence.Repositories;

namespace StudyAbroad.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "app"))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IStudyCenterRepository, StudyCenterRepository>();

        services.Configure<AdvisorOptions>(configuration.GetSection(AdvisorOptions.SectionName));
        services.AddHttpClient<IAdvisorClient, AdvisorClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AdvisorOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            if (!string.IsNullOrEmpty(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        return services;
    }
}
