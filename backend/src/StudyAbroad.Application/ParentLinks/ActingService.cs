using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

public enum ActingStartOutcome
{
    Ok,
    /// <summary>Phiên phụ huynh không hợp lệ (thiếu / sai refresh token, phiên đã thu hồi hoặc hết hạn). Trả 401.</summary>
    Unauthorized,
    /// <summary>Không phải con managed của phụ huynh này, hoặc phụ huynh không còn quyền. Trả 404 để không lộ con có tồn tại hay không.</summary>
    NotFound,
}

/// <summary>Ok thì có phiên mới của con, kèm hạn phiên phụ huynh để giữ cookie refresh của phụ huynh tới đúng hạn đó.</summary>
public record ActingStartResult(ActingStartOutcome Outcome, string? AccessToken = null, DateTime AccessExpiresAt = default,
    string? RefreshToken = null, DateTime SessionExpiresAt = default, DateTime ParentSessionExpiresAt = default)
{
    public static readonly ActingStartResult Unauthorized = new(ActingStartOutcome.Unauthorized);
    public static readonly ActingStartResult NotFound = new(ActingStartOutcome.NotFound);
}

/// <summary>1.5b: phụ huynh vào / thoát làm thay con managed.
/// Vào: tạo một phiên riêng cho con (token mang <c>act</c> của phụ huynh), hạn không quá hạn phiên phụ huynh.
/// Phiên phụ huynh giữ nguyên, refresh token của phụ huynh được cất sang cookie riêng để thoát thì lấy lại.</summary>
public class ActingService(
    IUserRepository users,
    ITokenService tokens,
    ActingSessionGuard guard,
    SessionRefreshService refresh,
    IAuditLogRepository auditLogs)
{
    /// <param name="parentAccessToken">Access token của phụ huynh đang dùng (để tìm đúng phiên).</param>
    /// <param name="parentRefreshToken">Refresh token của chính phiên đó. Bắt buộc khớp, nên token bị lộ một mình không mở được phiên làm thay.</param>
    public async Task<ActingStartResult> StartAsync(Guid parentId, Guid childId, string? parentAccessToken, string? parentRefreshToken,
        string? ipAddress, string? userAgent, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(parentAccessToken) || string.IsNullOrEmpty(parentRefreshToken))
            return ActingStartResult.Unauthorized;

        var now = DateTime.UtcNow;
        var parentSession = await users.GetSessionByTokenHashAsync(tokens.HashToken(parentAccessToken), ct);
        if (parentSession is null || parentSession.IsRevoked || parentSession.UserId != parentId || parentSession.ExpiresAt <= now
            || parentSession.RefreshTokenHash != tokens.HashToken(parentRefreshToken))
            return ActingStartResult.Unauthorized;

        var child = await guard.FindChildToActAsAsync(parentId, childId, ct);
        if (child is null)
            return ActingStartResult.NotFound;

        var (accessToken, accessExpiresAt) = tokens.GenerateToken(child, parentId);
        var refreshToken = RefreshTokens.Generate();
        var sessionExpiresAt = now.Add(RefreshTokens.Lifetime);
        if (sessionExpiresAt > parentSession.ExpiresAt)
            sessionExpiresAt = parentSession.ExpiresAt;

        await users.CreateSessionAsync(new UserSession
        {
            UserId = child.Id,
            TokenHash = tokens.HashToken(accessToken),
            RefreshTokenHash = tokens.HashToken(refreshToken),
            ExpiresAt = sessionExpiresAt,
            IsRevoked = false,
            IpAddress = ipAddress,
            UserAgent = userAgent,
        }, ct);
        await LogAsync(parentId, "parent.acting.start", child.Id, ct);

        return new ActingStartResult(ActingStartOutcome.Ok, accessToken, accessExpiresAt, refreshToken, sessionExpiresAt, parentSession.ExpiresAt);
    }

    /// <summary>Thoát làm thay: đóng phiên của con, gia hạn phiên phụ huynh bằng refresh token đã cất.
    /// Không có refresh token của phụ huynh thì trả null (không đang làm thay), không đụng phiên nào.</summary>
    public async Task<RefreshResult?> StopAsync(string? childAccessToken, string? childRefreshToken, string? parentRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(parentRefreshToken))
            return null;

        // Chỉ đóng phiên của con managed: cookie phụ huynh cũ còn sót không làm đăng xuất tài khoản thường
        Guid? childId = null;
        foreach (var session in new[]
                 {
                     string.IsNullOrEmpty(childAccessToken) ? null : await users.GetSessionByTokenHashAsync(tokens.HashToken(childAccessToken), ct),
                     string.IsNullOrEmpty(childRefreshToken) ? null : await users.GetSessionByRefreshTokenHashAsync(tokens.HashToken(childRefreshToken), ct),
                 })
        {
            if (session is null || (await users.GetUserById(session.UserId, ct))?.Status != UserStatuses.Managed)
                continue;
            await users.RevokeSessionByIdAsync(session.Id, ct);
            childId = session.UserId;
        }

        var parentSession = await users.GetSessionByRefreshTokenHashAsync(tokens.HashToken(parentRefreshToken), ct);
        var result = await refresh.RefreshAsync(parentRefreshToken, ct);
        if (result.Outcome == RefreshOutcome.Ok && parentSession is not null && childId is { } id)
            await LogAsync(parentSession.UserId, "parent.acting.stop", id, ct);

        return result;
    }

    // Như ManagedChildService: chỉ ghi id con, không ghi tên
    private Task LogAsync(Guid parentId, string action, Guid childId, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = parentId,
            Action = action,
            EntityType = "user",
            EntityId = childId.ToString(),
            DetailJson = JsonSerializer.Serialize(new { managedChildId = childId }),
        }, ct);
}
