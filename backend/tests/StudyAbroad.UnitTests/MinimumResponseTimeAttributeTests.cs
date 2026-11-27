using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Api.Filters;

namespace StudyAbroad.UnitTests;

/// <summary>Email có hay không có tài khoản thì đăng ký, gửi lại OTP, quên mật khẩu đều trả lời sau cùng một mức thời gian.</summary>
public class MinimumResponseTimeAttributeTests
{
    private static (ActionExecutingContext, ActionExecutionDelegate) Context(TimeSpan actionTime, HttpContext? http = null)
    {
        var action = new ActionContext(http ?? new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var executing = new ActionExecutingContext(action, new List<IFilterMetadata>(), new Dictionary<string, object?>(), new object());
        ActionExecutionDelegate next = async () =>
        {
            await Task.Delay(actionTime);
            return new ActionExecutedContext(action, new List<IFilterMetadata>(), new object());
        };
        return (executing, next);
    }

    [Fact]
    public async Task FastAction_IsPaddedToMinimum()
    {
        var (ctx, next) = Context(TimeSpan.Zero);
        var watch = Stopwatch.StartNew();
        await new MinimumResponseTimeAttribute(400).OnActionExecutionAsync(ctx, next);
        Assert.True(watch.ElapsedMilliseconds >= 390, $"chỉ chờ {watch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public async Task ClientDisconnect_StopsWaiting_WithoutThrowing()
    {
        using var cts = new CancellationTokenSource();
        var http = new DefaultHttpContext { RequestAborted = cts.Token };
        var (ctx, next) = Context(TimeSpan.Zero, http);
        cts.Cancel();
        var watch = Stopwatch.StartNew();
        await new MinimumResponseTimeAttribute(5000).OnActionExecutionAsync(ctx, next);
        // Không chờ đủ 5 giây khi người dùng đã ngắt kết nối
        Assert.True(watch.ElapsedMilliseconds < 4000);
    }

    [Theory]
    [InlineData(nameof(AuthController.Register))]
    [InlineData(nameof(AuthController.ResendOtp))]
    [InlineData(nameof(AuthController.ForgotPassword))]
    public void AuthEndpoints_ThatRevealAccountExistence_HaveMinimumTime(string method)
    {
        var attr = typeof(AuthController).GetMethod(method)!
            .GetCustomAttributes(typeof(MinimumResponseTimeAttribute), false);
        var min = Assert.IsType<MinimumResponseTimeAttribute>(Assert.Single(attr));
        Assert.Equal(400, min.Milliseconds);
    }
}
