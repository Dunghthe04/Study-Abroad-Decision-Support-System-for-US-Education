namespace StudyAbroad.Api.Extensions;

/// <summary>
/// Header bảo mật cho mọi phản hồi của API. Bản deploy chính thức (Cloudflare → Azure Container Apps) không đi qua Nginx,
/// nên header phải do chính API gắn; trước đây chỉ có ở deploy/nginx (Docker local).
/// API chỉ trả JSON/tệp, không có trang HTML, nên CSP chặn hết (default-src 'none') và cấm nhúng vào iframe.
/// Swagger UI là trang HTML cần script nên không gắn CSP cho /swagger.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
    public const string PermissionsPolicy = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";       // không cho trình duyệt đoán loại tệp (tệp tải lên bị hiểu là HTML)
        headers["X-Frame-Options"] = "DENY";                  // không trang nào được nhúng API vào iframe (clickjacking)
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = PermissionsPolicy;    // hệ thống không dùng camera, micro, vị trí, thanh toán trên trình duyệt

        if (!context.Request.Path.StartsWithSegments("/swagger"))
            headers["Content-Security-Policy"] = ApiContentSecurityPolicy;

        // Chỉ gửi HSTS qua HTTPS (sau UseForwardedHeaders, Cloudflare/Azure báo X-Forwarded-Proto: https)
        if (context.Request.IsHttps)
            headers["Strict-Transport-Security"] = "max-age=31536000";

        return next(context);
    }
}
