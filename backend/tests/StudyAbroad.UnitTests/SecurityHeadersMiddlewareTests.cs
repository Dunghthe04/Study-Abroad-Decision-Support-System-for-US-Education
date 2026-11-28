using Microsoft.AspNetCore.Http;
using StudyAbroad.Api.Extensions;

namespace StudyAbroad.UnitTests;

// Bản Azure không qua Nginx: API tự gắn header bảo mật cho mọi phản hồi
public class SecurityHeadersMiddlewareTests
{
    private static async Task<IHeaderDictionary> RunAsync(string path, bool https = true)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Request.Scheme = https ? "https" : "http";
        var called = false;
        await new SecurityHeadersMiddleware(_ => { called = true; return Task.CompletedTask; }).InvokeAsync(context);
        Assert.True(called);
        return context.Response.Headers;
    }

    [Fact]
    public async Task ApiResponse_GetsAllSecurityHeaders()
    {
        var headers = await RunAsync("/api/v1/auth/me");

        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
        Assert.Equal("DENY", headers["X-Frame-Options"]);
        Assert.Equal("strict-origin-when-cross-origin", headers["Referrer-Policy"]);
        Assert.Equal(SecurityHeadersMiddleware.PermissionsPolicy, headers["Permissions-Policy"]);
        Assert.Equal(SecurityHeadersMiddleware.ApiContentSecurityPolicy, headers["Content-Security-Policy"]);
        Assert.Equal("max-age=31536000", headers["Strict-Transport-Security"]);
    }

    [Fact]
    public async Task Swagger_HasNoCsp_SoSwaggerUiScriptsStillRun()
    {
        var headers = await RunAsync("/swagger/index.html");

        Assert.False(headers.ContainsKey("Content-Security-Policy"));
        Assert.Equal("nosniff", headers["X-Content-Type-Options"]);
    }

    [Fact]
    public async Task PlainHttp_HasNoHsts()
    {
        var headers = await RunAsync("/api/v1/auth/me", https: false);

        Assert.False(headers.ContainsKey("Strict-Transport-Security"));
        Assert.Equal("DENY", headers["X-Frame-Options"]);
    }
}
