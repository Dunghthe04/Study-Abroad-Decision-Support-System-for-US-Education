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
        StudentProfile? profile)
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
            summary = new SharedProfileSummaryDto(
                academic ? profile.OverallGpa : null,
                goals ? profile.TargetLevel : null,
                goals ? profile.IntendedMajor : null,
                academic ? profile.EducationSystem : null,
                academic ? profile.CurrentGrade : null,
                tests ? profile.Ielts ?? profile.Toefl ?? profile.Duolingo : null,
                tests ? profile.EnglishTestType : null,
                tests ? profile.Sat : null,
                tests ? profile.Act : null,
                budget ? profile.ExtracurricularTier : null,
                budget ? profile.BudgetTier : null);
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
}
