using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Extensions;

namespace StudyAbroad.Api.RateLimiting;

/// <summary>
/// Mỗi người chỉ một câu hỏi chat đang chạy (C13): pool chỉ có thể vượt hạn mức tối đa một lượt mỗi người.
/// Không xếp hàng; câu thứ hai trong lúc câu đầu chưa xong nhận 429 chat_busy.
/// </summary>
public static class ChatConcurrencyRateLimit
{
    public const string PolicyName = "chat-concurrency";
    public const string BusyCode = "chat_busy";
    public const string BusyMessage = "Bạn đang có một câu hỏi chưa trả lời xong. Vui lòng đợi.";

    public static IServiceCollection AddChatConcurrencyRateLimiter(this IServiceCollection services) =>
        services.AddRateLimiter(options => options.AddPolicy<string, Policy>(PolicyName));

    public sealed class Policy : IRateLimiterPolicy<string>
    {
        public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, ct) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.HttpContext.Response.WriteAsJsonAsync(new { code = BusyCode, message = BusyMessage }, ct);
        };

        public RateLimitPartition<string> GetPartition(HttpContext httpContext)
        {
            var key = httpContext.User.GetUserId()?.ToString()
                ?? $"ip:{httpContext.Connection.RemoteIpAddress}";
            return RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
        }
    }
}
