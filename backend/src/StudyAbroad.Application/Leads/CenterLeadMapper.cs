using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [UC-42 / UC-73] Tạo dữ liệu lead cho trung tâm, chỉ giữ các trường thuộc nhóm người gửi đã chọn
/// (<see cref="Lead.SharedFields"/>); trường thuộc nhóm không chọn trả null.
/// </summary>
public static class CenterLeadMapper
{
    public static CenterReceivedLeadDto ToCenterDto(
        Lead lead,
        string centerName,
        string? studentName,
        string? studentEmail,
        StudentProfile? profile,
        IReadOnlyList<ProfileCertification>? certifications = null,
        bool consentRevoked = false)
    {
        // [UC-80 / UC-73] Người gửi đã thu hồi (xóa tài khoản) hoặc yêu cầu quá 90 ngày: chỉ giữ mã, trung tâm, trạng thái,
        // ngày gửi; ẩn mọi dữ liệu người gửi đã chia sẻ
        // [UC-111 bước 5] Phụ huynh rút đồng ý của học sinh dưới 16: ẩn cả ở yêu cầu đã hoàn tất
        if (IsDataHidden(lead, consentRevoked))
            return new CenterReceivedLeadDto(
                lead.Id, lead.StudyCenterId, centerName,
                null, null, null, false,
                lead.Status, lead.CreatedAt,
                null, null, null)
            {
                DataHidden = true,
                ConsentRevoked = consentRevoked,
            };

        var fields = lead.SharedFields ?? [];
        bool Has(string group) => fields.Contains(group);

        var showContact = Has(LeadSharedFields.Contact);
        var academic = Has(LeadSharedFields.Academic);
        var tests = Has(LeadSharedFields.Tests);
        var goals = Has(LeadSharedFields.Goals);
        var budget = Has(LeadSharedFields.Budget);

        SharedProfileSummaryDto? summary = null;
        if (profile is not null && (academic || tests || goals || budget))
        {
            // Điểm đã thi đọc từ bảng chứng chỉ; dự định thi (dòng mục tiêu) để riêng ở PlannedTests
            var taken = certifications ?? [];
            var englishType = MainEnglishType(taken);
            summary = new SharedProfileSummaryDto(
                academic ? profile.OverallGpa : null,
                goals ? profile.TargetLevel : null,
                goals ? profile.IntendedMajor : null,
                academic ? profile.EducationSystem : null,
                academic ? profile.CurrentGrade : null,
                tests ? EnglishScore(taken, englishType) : null,
                tests ? englishType : null,
                tests ? BestTaken(taken, "sat") : null,
                tests ? BestTaken(taken, "act") : null,
                budget ? profile.ExtracurricularTier : null,
                budget ? profile.BudgetTier : null,
                tests ? PlannedTests(certifications) : null);
        }

        return new CenterReceivedLeadDto(
            lead.Id,
            lead.StudyCenterId,
            centerName,
            lead.StudyLevel,
            lead.ContactPhone,
            lead.Message,
            lead.SharedProfile,
            lead.Status,
            lead.CreatedAt,
            showContact ? studentName : null,
            showContact ? studentEmail : null,
            summary);
    }

    private static readonly string[] EnglishTypes =
    [
        ProfileConstants.EnglishTestTypes.Ielts,
        ProfileConstants.EnglishTestTypes.Toefl,
        ProfileConstants.EnglishTestTypes.Duolingo,
    ];

    // Chứng chỉ tiếng Anh chính: bài đã thi có ngày thi gần nhất (cùng ngày thì điểm cao hơn); chưa thi bài nào thì null
    private static string? MainEnglishType(IReadOnlyList<ProfileCertification> certifications) =>
        certifications
            .Where(c => !c.IsTarget && c.OverallScore is not null && EnglishTypes.Contains(c.TestType))
            .OrderByDescending(c => c.TestDate ?? DateOnly.MinValue)
            .ThenByDescending(c => c.OverallScore)
            .Select(c => c.TestType)
            .FirstOrDefault();

    private static decimal? EnglishScore(IReadOnlyList<ProfileCertification> certifications, string? englishType) =>
        englishType is null ? null : BestTaken(certifications, englishType);

    // Điểm cao nhất của loại thi trong các bài đã thi
    private static decimal? BestTaken(IReadOnlyList<ProfileCertification> certifications, string testType) =>
        certifications.Where(c => !c.IsTarget && c.TestType == testType).Max(c => c.OverallScore);

    private static IReadOnlyList<SharedPlannedTestDto>? PlannedTests(IReadOnlyList<ProfileCertification>? certifications) =>
        certifications?
            .Where(c => c.IsTarget)
            .OrderBy(c => c.SortOrder)
            .Select(c => new SharedPlannedTestDto(c.TestType, c.CustomName, c.Subject, c.OverallScore, c.ScaleText, c.TestDate))
            .ToList();

    /// <summary>[UC-73] Số ngày trung tâm còn xem được dữ liệu đã chia sẻ, tính từ lúc người dùng gửi yêu cầu.</summary>
    public const int DataVisibleDays = 90;

    /// <summary>
    /// [UC-73] Đã thu hồi hoặc quá 90 ngày: trung tâm không còn xem được dữ liệu đã chia sẻ.
    /// [UC-75 lớp 1b] Người gửi đã bị xóa hẳn (UserId null): lead chỉ còn để giữ tin nhắn, không còn dữ liệu người gửi.
    /// [UC-111 bước 5] <paramref name="consentRevoked"/>: học sinh dưới 16 không còn đồng ý của phụ huynh, ẩn ở mọi trạng thái;
    /// phụ huynh đồng ý lại thì yêu cầu chưa thu hồi hiện lại.
    /// </summary>
    public static bool IsDataHidden(Lead lead, bool consentRevoked = false) =>
        consentRevoked
        || lead.UserId is null
        || string.Equals(lead.Status, LeadStatuses.Withdrawn, StringComparison.OrdinalIgnoreCase)
        || lead.CreatedAt < DateTime.UtcNow.AddDays(-DataVisibleDays);
}
