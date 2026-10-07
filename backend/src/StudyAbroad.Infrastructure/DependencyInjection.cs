using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Infrastructure.Advisor;
using StudyAbroad.Infrastructure.Auth;
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
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IFinancialProfileRepository, FinancialProfileRepository>();
        services.AddScoped<StudyAbroad.Application.AcademicProfiles.IAcademicProfileRepository, AcademicProfileRepository>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        // [USAS-362] Đăng ký JWT Token Service (Ưu tiên nạp key từ biến môi trường JWT_SECRET_KEY)
        services.Configure<JwtOptions>(options =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(options);
            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                options.SecretKey = configuration["JWT_SECRET_KEY"]
                    ?? "usas_dev_jwt_secret_key_minimum_32_characters_for_local_development_2026!";
            }
        });
        services.AddSingleton<ITokenService, TokenService>();

        // [USAS-12 / USAS-362] Đăng ký Smtp Email Service (Nạp từ configuration hoặc biến môi trường SMTP_*)
        services.Configure<SmtpOptions>(options =>
        {
            configuration.GetSection(SmtpOptions.SectionName).Bind(options);
            options.Host = configuration["SMTP_HOST"] ?? options.Host;
            if (int.TryParse(configuration["SMTP_PORT"], out var port)) options.Port = port;
            options.User = configuration["SMTP_USER"] ?? options.User;
            options.Password = configuration["SMTP_PASSWORD"] ?? options.Password;
            options.FromEmail = configuration["SMTP_FROM_EMAIL"] ?? configuration["SMTP_SENDER_EMAIL"] ?? options.FromEmail;
            options.FromName = configuration["SMTP_FROM_NAME"] ?? configuration["SMTP_SENDER_NAME"] ?? options.FromName;
        });
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        services.Configure<AdvisorOptions>(configuration.GetSection(AdvisorOptions.SectionName));
        services.AddHttpClient<IAdvisorClient, AdvisorClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AdvisorOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            if (!string.IsNullOrEmpty(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        // [Scheduled Background Jobs: Unlock expired accounts & Cleanup OTPs]
        services.AddHostedService<StudyAbroad.Infrastructure.BackgroundJobs.AccountUnlockBackgroundService>();
        services.AddHostedService<StudyAbroad.Infrastructure.BackgroundJobs.OtpCleanupBackgroundService>();

        return services;
    }
}
