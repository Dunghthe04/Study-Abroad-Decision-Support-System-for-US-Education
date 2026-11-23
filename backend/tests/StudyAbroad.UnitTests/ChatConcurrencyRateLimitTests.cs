using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Api.RateLimiting;

namespace StudyAbroad.UnitTests;

// Một câu hỏi chat đang chạy mỗi người (C13); câu thứ hai nhận 429 chat_busy.
public class ChatConcurrencyRateLimitTests
{
    [Theory]
    [InlineData(nameof(AdvisorController.Chat))]
    [InlineData(nameof(AdvisorController.ChatStream))]
    public void ChatAndStream_ShareTheChatConcurrencyPolicy(string action)
    {
        var method = typeof(AdvisorController).GetMethod(action)!;

        Assert.Equal(ChatConcurrencyRateLimit.PolicyName, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
    }

    [Fact]
    public void SameUser_OneAtATime_OtherUsersUnaffected()
    {
        var policy = new ChatConcurrencyRateLimit.Policy();
        var userA = User(Guid.NewGuid());
        var partitionA = policy.GetPartition(userA);
        using var limiterA = partitionA.Factory(partitionA.PartitionKey);
        var partitionB = policy.GetPartition(User(Guid.NewGuid()));
        using var limiterB = partitionB.Factory(partitionB.PartitionKey);

        using (var running = limiterA.AttemptAcquire())
        {
            Assert.True(running.IsAcquired);
            using var second = limiterA.AttemptAcquire();
            Assert.False(second.IsAcquired);
            using var other = limiterB.AttemptAcquire();
            Assert.True(other.IsAcquired);
        }
        using var afterwards = limiterA.AttemptAcquire();
        Assert.True(afterwards.IsAcquired);
        Assert.Equal(partitionA.PartitionKey, policy.GetPartition(userA).PartitionKey);
    }

    [Fact]
    public async Task Rejection_Is429WithChatBusyCode()
    {
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        using var limiter = new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = 1, QueueLimit = 0 });
        using var running = limiter.AttemptAcquire();
        using var lease = limiter.AttemptAcquire();   // bị từ chối

        await new ChatConcurrencyRateLimit.Policy().OnRejected!(new OnRejectedContext { HttpContext = http, Lease = lease }, default);

        http.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(http.Response.Body);
        Assert.Equal(StatusCodes.Status429TooManyRequests, http.Response.StatusCode);
        Assert.Equal("chat_busy", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Bạn đang có một câu hỏi chưa trả lời xong. Vui lòng đợi.", body.RootElement.GetProperty("message").GetString());
    }

    private static DefaultHttpContext User(Guid id) => new()
    {
        User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")),
    };
}
