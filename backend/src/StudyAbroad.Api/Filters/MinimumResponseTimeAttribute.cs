using System.Diagnostics;
using Microsoft.AspNetCore.Mvc.Filters;

namespace StudyAbroad.Api.Filters;

/// <summary>
/// Giữ thời gian trả lời tối thiểu <see cref="Milliseconds"/> ms cộng 0–100 ms ngẫu nhiên.
/// Ở đăng ký, gửi lại OTP, quên mật khẩu: email có tài khoản thì phải lưu OTP và ghi mail, email không có thì trả ngay.
/// Đo được chênh lệch thời gian là biết email nào có tài khoản; chờ cho đủ một mức chung thì hai nhánh trông như nhau.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class MinimumResponseTimeAttribute(int milliseconds) : Attribute, IAsyncActionFilter
{
    private const int MaxJitterMs = 100;

    public int Milliseconds { get; } = milliseconds;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var watch = Stopwatch.StartNew();
        await next();

        var target = TimeSpan.FromMilliseconds(Milliseconds + Random.Shared.Next(0, MaxJitterMs + 1));
        var remaining = target - watch.Elapsed;
        if (remaining <= TimeSpan.Zero)
            return;
        try
        {
            await Task.Delay(remaining, context.HttpContext.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            // Người dùng đã ngắt kết nối: không còn ai đo thời gian
        }
    }
}
