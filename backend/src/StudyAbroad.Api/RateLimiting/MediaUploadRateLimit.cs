using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Api.MediaUploads;

namespace StudyAbroad.Api.RateLimiting;

public static class MediaUploadRateLimit
{
    public const string PolicyName = "media-image-upload";
    public const int PermitsPerWindow = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddMediaUploadRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options => options.AddPolicy<string, Policy>(PolicyName));

    public sealed class Policy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, ct) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                var seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }
            await context.HttpContext.Response.WriteAsJsonAsync(
                new MediaUploadError("Too many image uploads. Wait a minute and try again."), ct);
        };

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var key = httpContext.User.GetUserId()?.ToString()
                ?? $"ip:{httpContext.Connection.RemoteIpAddress}";
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
