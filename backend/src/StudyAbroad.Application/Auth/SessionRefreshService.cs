using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Auth;

public enum RefreshOutcome
{
    Ok,
    /// <summary>Request khác vừa gia hạn trước (hai tab cùng lúc). Trả 409, frontend gửi lại request với cookie mới.</summary>
    Retry,
    /// <summary>Không gia hạn được: không có token, sai, hết hạn, phiên đã thu hồi, tài khoản bị khóa. Trả 401.</summary>
    Unauthorized,
}

/// <summary>Ok thì có đủ cặp token mới. SessionExpiresAt là hạn cố định của phiên (hạn cookie refresh).</summary>
public record RefreshResult(RefreshOutcome Outcome, string? AccessToken = null, DateTime AccessExpiresAt = default,
    string? RefreshToken = null, DateTime SessionExpiresAt = default)
{
    public static readonly RefreshResult Retry = new(RefreshOutcome.Retry);
    public static readonly RefreshResult Unauthorized = new(RefreshOutcome.Unauthorized);
}

/// <summary>Gia hạn phiên bằng refresh token (OAuth 2.0 Security BCP, RFC 9700):
/// mỗi lần gia hạn đổi refresh token mới, token cũ dùng lại sau 30 giây thì coi là bị lộ và thu hồi cả phiên.
/// Phiên vẫn hết hạn sau 7 ngày từ lúc đăng nhập, gia hạn không kéo dài mốc này.</summary>
public class SessionRefreshService(IUserRepository users, ITokenService tokens, ActingSessionGuard acting)
{
    public async Task<RefreshResult> RefreshAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return RefreshResult.Unauthorized;

        var hash = tokens.HashToken(rawRefreshToken);
        var session = await users.GetSessionByRefreshTokenHashAsync(hash, ct);
        if (session is null || session.IsRevoked)
            return RefreshResult.Unauthorized;

        var now = DateTime.UtcNow;
        if (session.RefreshTokenHash != hash)
        {
            // Token vừa bị đổi. Trong 30 giây: tab chậm hơn, cho thử lại. Sau đó: có người giữ token cũ, thu hồi phiên.
            var rotatedAt = session.UpdatedAt ?? session.CreatedAt;
            if (now - rotatedAt <= RefreshTokens.ReuseGrace)
                return RefreshResult.Retry;

            await users.RevokeSessionByIdAsync(session.Id, ct);
            return RefreshResult.Unauthorized;
        }

        if (session.ExpiresAt <= now)
            return RefreshResult.Unauthorized;

        // Giống OnTokenValidated: bị Admin khóa hoặc đã xóa thì mất quyền, tạm khóa (temp_locked) thì vẫn dùng tiếp
        var user = await users.GetUserById(session.UserId, ct);
        if (user is null || user.Status == UserStatuses.Locked || user.Status == UserStatuses.Deleted)
            return RefreshResult.Unauthorized;

        // 1.5b: con managed chỉ có phiên làm thay. Gia hạn giữ claim act; phụ huynh mất quyền thì đóng phiên luôn.
        string accessToken;
        DateTime accessExpiresAt;
        if (user.Status == UserStatuses.Managed)
        {
            if (await acting.FindActingParentAsync(user, ct) is not { } parentId)
            {
                await users.RevokeSessionByIdAsync(session.Id, ct);
                return RefreshResult.Unauthorized;
            }
            (accessToken, accessExpiresAt) = tokens.GenerateToken(user, parentId);
        }
        else
        {
            (accessToken, accessExpiresAt) = tokens.GenerateToken(user);
        }
        var newRefreshToken = RefreshTokens.Generate();

        var rotated = await users.RotateRefreshTokenAsync(session.Id, hash, tokens.HashToken(newRefreshToken), tokens.HashToken(accessToken), ct);
        if (!rotated)
            return RefreshResult.Retry;

        return new RefreshResult(RefreshOutcome.Ok, accessToken, accessExpiresAt, newRefreshToken, session.ExpiresAt);
    }

    /// <summary>Đăng xuất khi access token đã hết hạn: thu hồi phiên theo refresh token.</summary>
    public async Task RevokeAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
            return;

        var session = await users.GetSessionByRefreshTokenHashAsync(tokens.HashToken(rawRefreshToken), ct);
        if (session is not null)
            await users.RevokeSessionByIdAsync(session.Id, ct);
    }
}
