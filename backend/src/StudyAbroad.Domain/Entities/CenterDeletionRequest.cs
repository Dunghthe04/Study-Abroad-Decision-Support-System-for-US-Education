using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-92 / UC-93] Yêu cầu xóa tài khoản và dữ liệu do trung tâm gửi; Administrator xử lý ở UC-93.
/// </summary>
public class CenterDeletionRequest : BaseEntity
{
    public Guid StudyCenterId { get; set; }
    /// <summary>Null sau khi tài khoản người gửi bị xóa hẳn (UC-93), bản ghi yêu cầu vẫn còn để lưu vết.</summary>
    public Guid? RequestedByUserId { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>pending | approved | completed | rejected (<see cref="Constants.CenterDeletionRequestStatuses"/>).</summary>
    public string Status { get; set; } = Constants.CenterDeletionRequestStatuses.Pending;

    public Guid? HandledByUserId { get; set; }
    public DateTime? HandledAt { get; set; }

    /// <summary>[UC-93] Ghi chú kiểm tra danh tính khi duyệt, hoặc lý do khi từ chối.</summary>
    public string? DecisionNote { get; set; }

    /// <summary>[UC-93] Ngày xóa hẳn dữ liệu (ngày duyệt + thời hạn chờ của UC-80).</summary>
    public DateTime? ScheduledPurgeAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// [UC-93] Hạn xử lý yêu cầu: 20 ngày kể từ khi nhận (Nghị định 356/2025/NĐ-CP Điều 5 khoản 4).
    /// Thời hạn này cũng là ngày sớm nhất được xóa hẳn dữ liệu.
    /// </summary>
    public DateTime? DueAt { get; set; }

    /// <summary>[UC-93] Lúc gia hạn. Chỉ được gia hạn một lần, tối đa 20 ngày (Điều 5 khoản 4).</summary>
    public DateTime? ExtendedAt { get; set; }

    /// <summary>[UC-93] Lý do gia hạn, đã gửi cho người yêu cầu.</summary>
    public string? ExtensionNote { get; set; }
}
