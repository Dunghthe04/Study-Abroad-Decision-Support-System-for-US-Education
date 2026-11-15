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
            // Cột cache Sat/Act lấy điểm mục tiêu khi chưa thi (để gợi ý trường). Có dòng chứng chỉ thì trung tâm chỉ nhận điểm đã thi,
            // dự định để riêng ở PlannedTests. Tiếng Anh: EnglishTestType chỉ đặt theo bài đã thi nên cột cùng loại là điểm thật
            var englishType = profile.EnglishTestType is { } t && t != ProfileConstants.EnglishTestTypes.None ? t : null;
            summary = new SharedProfileSummaryDto(
                academic ? profile.OverallGpa : null,
                goals ? profile.TargetLevel : null,
                goals ? profile.IntendedMajor : null,
                academic ? profile.EducationSystem : null,
                academic ? profile.CurrentGrade : null,
                tests ? EnglishScore(profile, englishType) : null,
                tests ? englishType : null,
                tests ? TakenScore(certifications, "sat", profile.Sat) : null,
                tests ? TakenScore(certifications, "act", profile.Act) : null,
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

    private static decimal? EnglishScore(StudentProfile profile, string? englishType) => englishType switch
    {
        ProfileConstants.EnglishTestTypes.Ielts => profile.Ielts,
        ProfileConstants.EnglishTestTypes.Toefl => profile.Toefl,
        ProfileConstants.EnglishTestTypes.Duolingo => profile.Duolingo,
        _ => null,
    };

    // Không truyền danh sách chứng chỉ (nơi gọi cũ) thì giữ cột cache
    private static decimal? TakenScore(IReadOnlyList<ProfileCertification>? certifications, string testType, decimal? column) =>
        certifications is null
            ? column
            : certifications.Where(c => !c.IsTarget && c.TestType == testType).Max(c => c.OverallScore);

    private static IReadOnlyList<SharedPlannedTestDto>? PlannedTests(IReadOnlyList<ProfileCertification>? certifications) =>
        certifications?
            .Where(c => c.IsTarget)
            .OrderBy(c => c.SortOrder)
            .Select(c => new SharedPlannedTestDto(c.TestType, c.CustomName, c.Subject, c.OverallScore, c.ScaleText, c.TestDate))
            .ToList();
}
