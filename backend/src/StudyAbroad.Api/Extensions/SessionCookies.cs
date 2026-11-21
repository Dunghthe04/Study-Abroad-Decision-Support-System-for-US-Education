namespace StudyAbroad.Api.Extensions;

/// <summary>Cookie phiên đăng nhập, dùng chung cho AuthController và ActingController.
/// [Chống XSS] token chỉ nằm trong HttpOnly Cookie, body không chứa token.</summary>
public static class SessionCookies
{
    public const string AccessToken = "usas_access_token";
    /// <summary>Refresh token chỉ gửi tới /api/v1/auth (SameSite=Strict), các API khác không thấy nó.</summary>
    public const string RefreshToken = "usas_refresh_token";
    /// <summary>1.5b: refresh token của phụ huynh được cất ở đây trong lúc làm thay con, thoát làm thay thì lấy lại.</summary>
    public const string ParentRefreshToken = "usas_parent_refresh_token";
    public const string AuthPath = "/api/v1/auth";

    /// <summary>Access token của request: HttpOnly Cookie trước, không có thì header Authorization.</summary>
    public static string ReadAccessToken(HttpRequest request)
    {
        var rawToken = request.Cookies[AccessToken];
        if (!string.IsNullOrEmpty(rawToken))
            return rawToken;

        var authHeader = request.Headers.Authorization.ToString();
        return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();
    }

    public static void AppendSession(HttpContext context, string accessToken, DateTime accessExpiresAt, string? refreshToken, DateTime? sessionExpiresAt)
    {
        context.Response.Cookies.Append(AccessToken, accessToken, AccessOptions(context, accessExpiresAt));
        if (refreshToken is not null && sessionExpiresAt is { } expires)
            context.Response.Cookies.Append(RefreshToken, refreshToken, AuthPathOptions(context, expires));
    }

    public static void AppendParentRefresh(HttpContext context, string parentRefreshToken, DateTime parentSessionExpiresAt) =>
        context.Response.Cookies.Append(ParentRefreshToken, parentRefreshToken, AuthPathOptions(context, parentSessionExpiresAt));

    /// <summary>Xóa cookie phiên. includeParent=false giữ cookie phụ huynh để còn thoát làm thay về được phiên phụ huynh.</summary>
    public static void ClearSession(HttpContext context, bool includeParent = true)
    {
        context.Response.Cookies.Delete(AccessToken, AccessOptions(context, expires: null));
        context.Response.Cookies.Delete(RefreshToken, AuthPathOptions(context, expires: null));
        if (includeParent)
            ClearParentRefresh(context);
    }

    public static void ClearParentRefresh(HttpContext context) =>
        context.Response.Cookies.Delete(ParentRefreshToken, AuthPathOptions(context, expires: null));

    // [Bảo mật] Ngoài Development cookie luôn Secure, không phụ thuộc Request.IsHttps (sau proxy có thể sai)
    private static bool IsSecure(HttpContext context) =>
        !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() || context.Request.IsHttps;

    private static CookieOptions AccessOptions(HttpContext context, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = IsSecure(context),
        SameSite = SameSiteMode.Lax,
        Expires = expires,
        Path = "/"
    };

    // Refresh token: Strict và chỉ gửi tới /api/v1/auth, nên trang khác không kéo được nó theo
    private static CookieOptions AuthPathOptions(HttpContext context, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = IsSecure(context),
        SameSite = SameSiteMode.Strict,
        Expires = expires,
        Path = AuthPath
    };
}
