using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Application;
using StudyAbroad.Application.Auth;
using StudyAbroad.Infrastructure;
using StudyAbroad.Infrastructure.Auth;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) => config
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// [USAS-362] Cấu hình JWT Bearer Authentication & Quản lý thu hồi phiên (AC-3)
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

// [USAS-362 Bảo mật] Nạp SecretKey từ biến môi trường JWT_SECRET_KEY hoặc cấu hình
if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey))
{
    jwtOptions.SecretKey = builder.Configuration["JWT_SECRET_KEY"] ?? string.Empty;
}

// [Bảo mật nghiêm ngặt] Kiểm tra độ an toàn của khóa ký số:
if (string.IsNullOrWhiteSpace(jwtOptions.SecretKey) || jwtOptions.SecretKey.Length < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        jwtOptions.SecretKey = "usas_dev_jwt_secret_key_minimum_32_characters_for_local_development_2026!";
    }
    else
    {
        throw new InvalidOperationException(
            "CRITICAL SECURITY: JWT SecretKey must be configured via environment variable (JWT_SECRET_KEY) with minimum 32 characters in Production.");
    }
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
        ClockSkew = TimeSpan.Zero
    };

    // [AC-3] Kiểm tra phiên trong DB: nếu đã bị thu hồi (IsRevoked == true) thì từ chối request
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            var tokenService = context.HttpContext.RequestServices.GetRequiredService<ITokenService>();
            var userRepo = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();

            var authHeader = context.Request.Headers.Authorization.ToString();
            var rawToken = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader["Bearer ".Length..].Trim()
                : authHeader.Trim();

            if (!string.IsNullOrEmpty(rawToken))
            {
                var tokenHash = tokenService.HashToken(rawToken);
                var session = await userRepo.GetSessionByTokenHashAsync(tokenHash);
                if (session == null || session.IsRevoked)
                {
                    context.Fail("Phiên đăng nhập đã hết hiệu lực.");
                }
            }
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Nhập JWT token theo cú pháp: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Default")!, name: "postgres", tags: ["ready"]);

// Running behind Nginx: trust X-Forwarded-* headers from the reverse proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapAppHealthChecks();

await app.ApplyDatabaseMigrationsAsync();

app.Run();

// Exposed for integration tests (WebApplicationFactory).
public partial class Program;
