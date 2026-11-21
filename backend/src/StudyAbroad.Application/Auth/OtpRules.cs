using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Auth;

/// <summary>
/// Quy tắc OTP dùng chung cho AuthService (người dùng) và CenterAccountService (trung tâm):
/// đối chiếu mã một lần, giới hạn số lần thử, giãn cách giữa hai lần gửi và hạn mức theo giờ.
/// </summary>
internal static class OtpRules
{
    /// <summary>
    /// Đối chiếu mã OTP mới nhất của user. Tăng bộ đếm lần thử và lưu TRƯỚC khi so mã: nhiều request song song
    /// thì chỉ một request ghi được (Version/xmin), các request còn lại thất bại mà không được so mã.
    /// Đúng mã thì đánh dấu đã dùng (dùng 1 lần); sai đủ 5 lần thì vô hiệu hóa mã.
    /// </summary>
    public static async Task<bool> TryConsumeAsync(IUserRepository users, Guid userId, string purpose, string? code, CancellationToken ct)
    {
        var otp = await users.GetLatestOtpAsync(userId, purpose, ct);
        if (otp == null || otp.UsedAt != null || DateTime.UtcNow > otp.ExpiresAt || otp.Attempts >= AuthService.MaxOtpAttempts)
            return false;

        otp.Attempts++;
        if (!await TryUpdateAsync(users, otp, ct))
            return false;

        if (string.IsNullOrWhiteSpace(code) || otp.CodeHash != AuthService.HashOtp(code))
        {
            if (otp.Attempts >= AuthService.MaxOtpAttempts)
            {
                otp.UsedAt = DateTime.UtcNow; // Vô hiệu hóa mã sau 5 lần sai
                await TryUpdateAsync(users, otp, ct);
            }
            return false;
        }

        // Đúng mã -> đánh dấu đã dùng; request song song khác dùng cùng mã sẽ bị từ chối
        otp.UsedAt = DateTime.UtcNow;
        return await TryUpdateAsync(users, otp, ct);
    }

    /// <summary>Kiểm tra cooldown 60 giây giữa 2 lần gửi và giới hạn 5 lần/giờ.</summary>
    public static async Task<bool> CanSendAsync(IUserRepository users, Guid userId, string purpose, CancellationToken ct)
    {
        var latest = await users.GetLatestOtpAsync(userId, purpose, ct);
        if (latest != null && (DateTime.UtcNow - latest.CreatedAt).TotalSeconds < AuthService.MinSecondsBetweenOtpRequests)
            return false;

        return await users.CountOtpRequestsInLastHourAsync(userId, purpose, ct) < AuthService.MaxOtpRequestsPerHour;
    }

    public static async Task<bool> TryUpdateAsync(IUserRepository users, OtpToken otp, CancellationToken ct)
    {
        try
        {
            await users.UpdateOtpAsync(otp, ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }
}
