using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [USAS-2053 / UC-74] Hàng dữ liệu trung gian truy vấn từ app.lead_messages và app.users
/// để chuẩn hóa hiển thị và che nội dung nhạy cảm nhất quán giữa repository và service.
/// </summary>
public sealed record RawLeadMessageRow(
    Guid Id,
    Guid LeadId,
    Guid SenderUserId,
    string Content,
    bool IsFlagged,
    string? FlagReason,
    string Status,
    DateTime CreatedAt,
    string SenderRole,
    string? SenderFullName);

/// <summary>
/// [USAS-2053 / UC-74] Bộ chuyển đổi và truy vấn chuẩn cho luồng tin nhắn yêu cầu liên hệ.
/// </summary>
public static class LeadMessageMapper
{
    public const string RemovedContentPlaceholder = "Tin nhắn đã bị gỡ do vi phạm quy tắc.";
    public const string AnonymousStudentName = "Học sinh / Phụ huynh (Ẩn danh)";
    public const string DefaultCenterName = "Trung tâm tư vấn";

    /// <summary>
    /// Xác định vai trò thuộc phía trung tâm hoặc đội ngũ quản trị/kiểm duyệt hệ thống.
    /// </summary>
    public static bool IsStaffOrCenterRole(string? role) =>
        role is UserRoles.Center or UserRoles.Moderator or UserRoles.Admin;

    /// <summary>
    /// Xác định tên hiển thị hợp lệ theo vai trò người gửi và trạng thái cấp quyền chia sẻ hồ sơ (SharedProfile).
    /// </summary>
    public static string ResolveSenderDisplayName(
        string? senderRole,
        string? senderFullName,
        bool sharedProfile,
        string? centerName)
    {
        if (senderRole == UserRoles.Center)
            return string.IsNullOrWhiteSpace(centerName) ? DefaultCenterName : centerName.Trim();

        if (senderRole == UserRoles.Moderator)
            return "Kiểm duyệt viên";

        if (senderRole == UserRoles.Admin)
            return "Quản trị viên";

        // [Q11 / UC-73 / UC-74] Chỉ hiển thị họ tên học sinh/phụ huynh nếu người gửi đã bật SharedProfile
        return sharedProfile && !string.IsNullOrWhiteSpace(senderFullName)
            ? senderFullName.Trim()
            : AnonymousStudentName;
    }

    /// <summary>
    /// Truy vấn thô luồng tin nhắn theo thứ tự thời gian tăng dần kèm thông tin vai trò/tên người gửi (EF-compatible LINQ).
    /// </summary>
    public static IQueryable<RawLeadMessageRow> QueryThreadRows(
        IQueryable<LeadMessage> messages,
        IQueryable<User> users,
        Guid leadId) =>
        from m in messages
        where m.LeadId == leadId
        join u in users on m.SenderUserId equals u.Id into senderJoin
        from u in senderJoin.DefaultIfEmpty()
        orderby m.CreatedAt ascending
        select new RawLeadMessageRow(
            m.Id,
            m.LeadId,
            m.SenderUserId,
            m.Content,
            m.IsFlagged,
            m.FlagReason,
            m.Status,
            m.CreatedAt,
            u != null ? u.Role : UserRoles.Student,
            u != null ? u.FullName : null);

    /// <summary>
    /// Chuyển đổi một bản ghi truy vấn thô sang DTO, tự động che nội dung/lý do nếu tin nhắn đã bị gỡ.
    /// </summary>
    public static LeadMessageDto ToDto(
        RawLeadMessageRow row,
        bool sharedProfile,
        string? centerName)
    {
        var isFromCenter = IsStaffOrCenterRole(row.SenderRole);
        var senderName = ResolveSenderDisplayName(row.SenderRole, row.SenderFullName, sharedProfile, centerName);
        var isRemoved = string.Equals(row.Status, "removed", StringComparison.OrdinalIgnoreCase);

        return new LeadMessageDto(
            row.Id,
            row.LeadId,
            row.SenderUserId,
            senderName,
            row.SenderRole,
            isFromCenter,
            isRemoved ? RemovedContentPlaceholder : row.Content,
            row.IsFlagged,
            isRemoved ? null : row.FlagReason,
            row.Status,
            row.CreatedAt);
    }

    /// <summary>
    /// Chuyển đổi trực tiếp entity vừa lưu sang DTO trả về ngay cho client.
    /// </summary>
    public static LeadMessageDto ToDto(
        LeadMessage entity,
        string senderRole,
        string? senderFullName,
        bool sharedProfile,
        string? centerName) =>
        ToDto(
            new RawLeadMessageRow(
                entity.Id,
                entity.LeadId,
                entity.SenderUserId,
                entity.Content,
                entity.IsFlagged,
                entity.FlagReason,
                entity.Status,
                entity.CreatedAt,
                senderRole,
                senderFullName),
            sharedProfile,
            centerName);
}
