using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace StudyAbroad.Api.RateLimiting;

/// <summary>
/// [USAS-640] Giới hạn upload bảng điểm: mỗi lần upload chiếm GPU (OCR) từ vài chục giây tới vài phút,
/// nên mỗi user chỉ được tối đa <see cref="PermitsPerWindow"/> lần trong <see cref="Window"/>
/// và chỉ 1 request đang chạy cùng lúc. Vượt giới hạn trả 429.
/// </summary>
public static class TranscriptUploadRateLimit
{
    public const string PolicyName = "transcript-upload";
    public const int PermitsPerWindow = 5;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    public static IServiceCollection AddTranscriptUploadRateLimiter(this IServiceCollection services)
    {
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Bạn tải bảng điểm quá nhiều lần hoặc đang có một tệp được xử lý. Vui lòng đợi vài phút rồi thử lại."
                }, ct);
            };
            options.AddPolicy<string, Policy>(PolicyName);
        });
    }

    private sealed class Policy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => null;

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            // Action đã [Authorize] nên luôn có userId; dự phòng theo IP nếu thiếu
            var key = httpContext.User.GetUserId()?.ToString()
                ?? $"ip:{httpContext.Connection.RemoteIpAddress}";
            return RateLimitPartition.Get(key, _ => new PerUserUploadLimiter());
        }
    }

    /// <summary>Ghép giới hạn số lần theo cửa sổ thời gian với giới hạn 1 request đồng thời, không xếp hàng chờ</summary>
    public sealed class PerUserUploadLimiter : RateLimiter
    {
        private readonly ConcurrencyLimiter _concurrency = new(new ConcurrencyLimiterOptions
        {
            PermitLimit = 1,
            QueueLimit = 0,
        });

        private readonly FixedWindowRateLimiter _window = new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = PermitsPerWindow,
            Window = Window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });

        public override TimeSpan? IdleDuration =>
            _concurrency.IdleDuration is { } c && _window.IdleDuration is { } w ? (c < w ? c : w) : null;

        public override RateLimiterStatistics? GetStatistics() => _window.GetStatistics();

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            var concurrencyLease = _concurrency.AttemptAcquire(permitCount);
            if (!concurrencyLease.IsAcquired)
                return concurrencyLease;

            var windowLease = _window.AttemptAcquire(permitCount);
            if (!windowLease.IsAcquired)
            {
                concurrencyLease.Dispose();
                return windowLease;
            }

            return new CombinedLease(concurrencyLease, windowLease);
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
            ValueTask.FromResult(AttemptAcquireCore(permitCount));

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _concurrency.Dispose();
                _window.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    private sealed class CombinedLease(RateLimitLease first, RateLimitLease second) : RateLimitLease
    {
        public override bool IsAcquired => true;

        public override IEnumerable<string> MetadataNames => [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            metadata = null;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                first.Dispose();
                second.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
