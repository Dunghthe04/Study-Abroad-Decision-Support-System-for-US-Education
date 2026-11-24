using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Policies;

/// <summary>
/// [UC-104] Đồng ý Điều khoản và Chính sách bảo mật lúc ĐĂNG KÝ (học sinh, phụ huynh, trung tâm).
/// Tách hai bước để dịch vụ đăng ký gọi đúng lúc: kiểm tra TRƯỚC khi tạo tài khoản (sai thì không tạo gì),
/// ghi bằng chứng CÙNG giao dịch với việc tạo tài khoản (không bao giờ có tài khoản thiếu bằng chứng).
/// </summary>
public interface IRegistrationConsent
{
    /// <summary>Null nếu hợp lệ; ngược lại là câu báo lỗi tiếng Việt cho người đăng ký. Không ghi gì.</summary>
    Task<string?> ValidateAsync(IReadOnlyList<AcceptPolicyItem>? accepted, CancellationToken ct = default);

    /// <summary>Ghi mỗi mục đã đồng ý thành một dòng consents của tài khoản mới (đúng phiên bản người đó đã đọc, kèm IP).</summary>
    Task RecordAsync(Guid userId, IReadOnlyList<AcceptPolicyItem>? accepted, string? ipAddress, CancellationToken ct = default);
}

public class RegistrationConsent(IPublishedPolicyReader policies, IConsentRepository consents) : IRegistrationConsent
{
    public const string MissingMessage = "Vui lòng đồng ý với Điều khoản sử dụng và Chính sách bảo mật để tiếp tục.";
    public const string InvalidMessage = "Dữ liệu đồng ý không hợp lệ. Vui lòng tải lại trang và thử lại.";

    public async Task<string?> ValidateAsync(IReadOnlyList<AcceptPolicyItem>? accepted, CancellationToken ct = default)
    {
        var items = accepted ?? [];

        // Chỉ nhận hai loại của tài khoản, mỗi loại một lần
        if (items.Any(i => !PolicyAcceptanceRules.AccountKinds.Contains(i.Kind, StringComparer.Ordinal))
            || items.Select(i => i.Kind).Distinct(StringComparer.Ordinal).Count() != items.Count)
            return InvalidMessage;

        foreach (var kind in PolicyAcceptanceRules.AccountKinds)
        {
            var item = items.FirstOrDefault(i => i.Kind == kind);
            var current = (await policies.ListPublishedAsync(kind, ct)).Select(p => p.Version).Max(StringComparer.Ordinal);

            // Loại chưa công bố thì không có gì để đồng ý, nhưng cũng không nhận bản ghi cho một phiên bản không tồn tại
            if (current is null)
            {
                if (item is not null) return PolicyAcceptanceService.PolicyChangedMessage;
                continue;
            }

            if (item is null || string.IsNullOrWhiteSpace(item.Version)) return MissingMessage;
            // Người đăng ký đọc bản cũ trong lúc admin vừa công bố bản mới: bắt đọc lại để bằng chứng khớp văn bản đã đọc
            if (!string.Equals(item.Version, current, StringComparison.Ordinal)) return PolicyAcceptanceService.PolicyChangedMessage;
        }

        return null;
    }

    public async Task RecordAsync(Guid userId, IReadOnlyList<AcceptPolicyItem>? accepted, string? ipAddress, CancellationToken ct = default)
    {
        foreach (var item in accepted ?? [])
        {
            if (!PolicyAcceptanceRules.AccountKinds.Contains(item.Kind, StringComparer.Ordinal)) continue;
            await consents.AddAsync(new Consent
            {
                UserId = userId,
                Purpose = item.Kind,
                Granted = true,
                PolicyVersion = item.Version,
                GrantedIp = ipAddress,
            }, ct);
        }
    }
}
