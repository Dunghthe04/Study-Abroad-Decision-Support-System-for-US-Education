using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Family;

/// <summary>
/// Token trong link mời Gia đình: cùng cách làm với lời mời phụ huynh (<see cref="ParentInvite"/>), chỉ gửi qua mail, DB lưu SHA-256 hex.
/// Giới hạn: hạn 7 ngày kể từ lần gửi gần nhất, gửi lại cách nhau 60 giây, tối đa 5 lần trong 24 giờ.
/// </summary>
public static class FamilyInviteToken
{
    public static string Generate() => ParentInvite.GenerateToken();

    /// <summary>Luôn băm bằng hàm này (chữ HOA, đã Trim) khi tìm lời mời theo token.</summary>
    public static string Hash(string token) => ParentInvite.HashToken(token);

    /// <summary>
    /// Sinh token mới cho lời mời (token cũ chết vì hash bị thay), đặt hạn, đếm lần gửi, ghi giờ gửi.
    /// Bộ đếm reset khi lần gửi trước đã quá 24 giờ. Trả token gốc để gửi mail.
    /// </summary>
    public static string Issue(FamilyMember member, DateTime nowUtc)
    {
        if (member.LastInviteSentAt is { } last && nowUtc - last >= TimeSpan.FromDays(1))
            member.InviteSentCount = 0;

        var token = Generate();
        member.TokenHash = Hash(token);
        member.ExpiresAt = nowUtc.AddDays(BillingRules.InviteExpiryDays);
        member.InviteSentCount++;
        member.LastInviteSentAt = nowUtc;
        return token;
    }

    /// <summary>
    /// Lúc sớm nhất gửi lại được: muộn hơn của (lần gửi cuối + 60 giây) và, khi đã gửi đủ 5 lần trong 24 giờ, (lần gửi cuối + 24 giờ).
    /// Null = gửi lại được ngay. Dùng chung cho kiểm tra 429 và cho CanResendAt của GET để hai chỗ không lệch nhau.
    /// </summary>
    public static DateTime? NextResendAt(FamilyMember member, DateTime nowUtc)
    {
        if (member.LastInviteSentAt is not { } last)
            return null;
        var next = last.AddSeconds(BillingRules.InviteResendCooldownSeconds);
        if (member.InviteSentCount >= BillingRules.InviteMaxSendsPer24h && nowUtc - last < TimeSpan.FromDays(1))
        {
            var unlock = last.AddDays(1);
            if (unlock > next)
                next = unlock;
        }
        return next > nowUtc ? next : null;
    }
}
