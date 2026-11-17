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
        IReadOnlyList<ProfileCertification>? certifications = null)
    {
        // [UC-80] Người gửi đã xóa tài khoản: chỉ giữ mã, trung tâm, trạng thái, ngày gửi; ẩn mọi dữ liệu của người gửi
        if (string.Equals(lead.Status, LeadStatuses.Withdrawn, StringComparison.OrdinalIgnoreCase))
            return new CenterReceivedLeadDto(
                lead.Id, lead.StudyCenterId, centerName,
                null, null, null, false,
                lead.Status, lead.CreatedAt,
                null, null, null);

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
}
