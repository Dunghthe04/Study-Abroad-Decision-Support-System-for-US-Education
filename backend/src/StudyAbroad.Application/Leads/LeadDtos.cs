namespace StudyAbroad.Application.Leads;

/// <summary>
/// UC-42. <paramref name="StudentUserId"/>: bắt buộc khi phụ huynh gửi thay con, học sinh tự gửi thì bỏ qua.
/// <paramref name="SharedFields"/>: key trong LeadSharedFields, bắt buộc có "contact".
/// <paramref name="AcknowledgedCenterContact"/>: học sinh 16–17 tuổi phải tích.
/// <paramref name="SharedProfile"/>: giữ cho client cũ, server tự tính lại từ SharedFields.
/// </summary>
public record CreateLeadRequest(
    Guid StudyCenterId,
    string? StudyLevel = null,
    string? ContactPhone = null,
    string? Message = null,
    bool SharedProfile = false,
    Guid? StudentUserId = null,
    IReadOnlyList<string>? SharedFields = null,
    bool ConsentAccepted = false,
    bool AcknowledgedCenterContact = false);

public record LeadDto(
    Guid Id,
    Guid UserId,
    Guid StudyCenterId,
    string? StudyCenterName,
    Guid? ConsentId,
    string? StudyLevel,
    string? ContactPhone,
    string? Message,
    bool SharedProfile,
    string Status,
    DateTime CreatedAt)
{
    // UC-42. Để dạng init (không phải tham số có mặc định) vì LeadRepository dựng LeadDto trong truy vấn EF,
    // mà cây biểu thức không cho dùng tham số tùy chọn.
    public Guid? SubmittedByUserId { get; init; }
    public IReadOnlyList<string> SharedFields { get; init; } = [];
}

/// <summary>
/// [UC-44] Một yêu cầu liên hệ đã gửi, hiện ở thẻ "Yêu cầu liên hệ đã gửi" của học sinh / phụ huynh.
/// <paramref name="StudentUserId"/>, <paramref name="StudentName"/>: học sinh có hồ sơ (phụ huynh cần biết lead của con nào).
/// <paramref name="SubmittedByParent"/>: true khi người bấm gửi khác học sinh (phụ huynh gửi thay).
/// <paramref name="CenterMessageCount"/>: số tin nhắn đang hiện do phía trung tâm gửi.
/// </summary>
public record SentLeadDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<string> SharedFields,
    string? StudyLevel,
    string? ContactPhone,
    string? Message,
    Guid StudentUserId,
    string StudentName,
    bool SubmittedByParent,
    int CenterMessageCount);

/// <summary>
/// [UC-73] Tóm tắt hồ sơ học sinh được chia sẻ cho trung tâm (chỉ khi SharedProfile = true).
/// Tuân thủ quy định bảo vệ dữ liệu trẻ em và quyền riêng tư (Nghị định 356/2025/NĐ-CP).
/// </summary>
public record SharedProfileSummaryDto(
    decimal? OverallGpa,
    string? TargetLevel,
    string? IntendedMajor,
    string? EducationSystem,
    string? CurrentGrade,
    decimal? EnglishScore,
    string? EnglishTestType,
    decimal? SatScore,
    decimal? ActScore,
    int? ExtracurricularTier,
    string? BudgetTier,
    IReadOnlyList<SharedPlannedTestDto>? PlannedTests = null);

/// <summary>
/// Một dự định thi của học sinh (dòng IsTarget trong profile_certifications): điểm là mục tiêu, ngày là tháng dự kiến.
/// Tách khỏi điểm đã thi để trung tâm không đọc nhầm điểm mục tiêu thành điểm thật.
/// </summary>
public record SharedPlannedTestDto(
    string TestType,
    string? CustomName,
    string? Subject,
    decimal? TargetScore,
    string? TargetScaleText,
    DateOnly? PlannedDate);

/// <summary>
/// [UC-73] Chi tiết yêu cầu liên hệ nhận được phía trung tâm tư vấn.
/// Chỉ có các trường thuộc nhóm người gửi đã chọn (Lead.SharedFields, xem CenterLeadMapper):
/// thiếu <c>contact</c> thì StudentName, StudentEmail null; không chọn nhóm hồ sơ nào thì ProfileSummary null.
/// </summary>
public record CenterReceivedLeadDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    string? StudyLevel,
    string? ContactPhone,
    string? Message,
    bool SharedProfile,
    string Status,
    DateTime CreatedAt,
    string? StudentName,
    string? StudentEmail,
    SharedProfileSummaryDto? ProfileSummary);

/// <summary>
/// [UC-73] Danh sách phân trang kèm bộ đếm trạng thái dành cho cổng trung tâm.
/// </summary>
public record CenterLeadsSummaryDto(
    IReadOnlyList<CenterReceivedLeadDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int NewCount,
    int ContactedCount,
    int ClosedCount);

/// <summary>
/// [UC-73] Yêu cầu cập nhật trạng thái liên hệ (new | contacted | closed).
/// </summary>
public record UpdateLeadStatusRequest(string Status);

/// <summary>
/// [USAS-2053 / UC-74] Một tin nhắn trong luồng trao đổi của yêu cầu liên hệ.
/// Nội dung đã bị gỡ (Status = "removed") được ẩn bởi lớp truy vấn;
/// tên học sinh/phụ huynh được ẩn danh nếu Lead chưa bật SharedProfile.
/// </summary>
public record LeadMessageDto(
    Guid Id,
    Guid LeadId,
    Guid SenderUserId,
    string SenderName,
    string SenderRole,
    bool IsFromCenter,
    string Content,
    bool IsFlagged,
    string? FlagReason,
    string Status,
    DateTime CreatedAt);

/// <summary>
/// [USAS-2053 / UC-74] Toàn bộ luồng tin nhắn của một yêu cầu liên hệ kèm trạng thái hiện tại.
/// </summary>
public record LeadMessagesThreadDto(
    Guid LeadId,
    string LeadStatus,
    IReadOnlyList<LeadMessageDto> Messages,
    int FlaggedCount);

/// <summary>
/// [USAS-2053 / UC-74] Yêu cầu gửi tin nhắn trả lời từ phía Trung tâm tư vấn.
/// </summary>
public record SendLeadMessageRequest(string Content);

/// <summary>
/// [USAS-2053 / UC-74] Kết quả sau khi gửi tin nhắn thành công, kèm trạng thái mới nhất của yêu cầu liên hệ.
/// </summary>
public record SendLeadMessageResultDto(
    LeadMessageDto Message,
    string LeadStatus);



