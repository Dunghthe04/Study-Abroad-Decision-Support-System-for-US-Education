using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace StudyAbroad.Api.RateLimiting;

/// <summary>
/// [Bảo mật UC-01→06] Giới hạn theo IP cho các API xác thực (đăng nhập, đăng ký, OTP, quên/đổi mật khẩu)
/// để chống dò mật khẩu, dò OTP và spam mail: mỗi IP tối đa <see cref="PermitsPerWindow"/> lần
/// cho mỗi API trong <see cref="Window"/>. Vượt giới hạn trả 429. Nginx còn một lớp giới hạn nữa (zone auth).
/// </summary>
public static class AuthRateLimit
{
    public const string PolicyName = "auth";
    public const int PermitsPerWindow = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddAuthRateLimiter(this IServiceCollection services)
    {
        return services.AddRateLimiter(options => options.AddPolicy<string, Policy>(PolicyName));
    }

    private sealed class Policy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, ct) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
            }

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Bạn thao tác quá nhiều lần. Vui lòng đợi một phút rồi thử lại."
            }, ct);
        };

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            // Sau UseForwardedHeaders, RemoteIpAddress là IP thật của client do Nginx gắn vào X-Forwarded-For
            var key = $"{httpContext.Connection.RemoteIpAddress}|{httpContext.Request.Path.Value?.ToLowerInvariant()}";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = PermitsPerWindow,
                Window = Window,
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        }
    }
}
