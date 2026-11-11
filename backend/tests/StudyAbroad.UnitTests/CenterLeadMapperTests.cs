using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-42 / UC-73] Trung tâm chỉ thấy các trường thuộc nhóm dữ liệu người gửi đã chọn.</summary>
public class CenterLeadMapperTests
{
    private static readonly StudentProfile Profile = new()
    {
        OverallGpa = 3.8m,
        EducationSystem = "specialized",
        CurrentGrade = "Lớp 12",
        Ielts = 7.5m,
        EnglishTestType = "ielts",
        Sat = 1480m,
        Act = 32m,
        TargetLevel = "bachelor",
        IntendedMajor = "Computer Science",
        ExtracurricularTier = 2,
        BudgetTier = "tier_35k_50k",
    };

    private static CenterReceivedLeadDto Map(params string[] fields) =>
        CenterLeadMapper.ToCenterDto(
            new Lead { SharedFields = [.. fields], ContactPhone = "0912345678" },
            "Trung Tâm Test",
            "Nguyen Van A",
            "a@example.com",
            Profile);

    [Fact]
    public void ContactAndGoals_CenterSeesNameAndGoalsButNoGpa()
    {
        var dto = Map(LeadSharedFields.Contact, LeadSharedFields.Goals);

        Assert.Equal("Nguyen Van A", dto.StudentName);
        Assert.Equal("a@example.com", dto.StudentEmail);
        var p = Assert.IsType<SharedProfileSummaryDto>(dto.ProfileSummary);
        Assert.Equal("bachelor", p.TargetLevel);
        Assert.Equal("Computer Science", p.IntendedMajor);
        Assert.Null(p.OverallGpa);
        Assert.Null(p.EducationSystem);
        Assert.Null(p.CurrentGrade);
        Assert.Null(p.EnglishScore);
        Assert.Null(p.SatScore);
        Assert.Null(p.BudgetTier);
        Assert.Null(p.ExtracurricularTier);
    }

    [Fact]
    public void ContactOnly_NoProfileSummary()
    {
        var dto = Map(LeadSharedFields.Contact);

        Assert.Equal("Nguyen Van A", dto.StudentName);
        Assert.Null(dto.ProfileSummary);
        Assert.Equal("0912345678", dto.ContactPhone);
    }

    [Fact]
    public void OldLeadWithoutGroups_HidesNameEmailAndProfile()
    {
        var dto = Map();

        Assert.Null(dto.StudentName);
        Assert.Null(dto.StudentEmail);
        Assert.Null(dto.ProfileSummary);
    }

    [Fact]
    public void AllGroups_CenterSeesEveryField()
    {
        var dto = Map([.. LeadSharedFields.All]);

        var p = Assert.IsType<SharedProfileSummaryDto>(dto.ProfileSummary);
        Assert.Equal(3.8m, p.OverallGpa);
        Assert.Equal("Lớp 12", p.CurrentGrade);
        Assert.Equal(7.5m, p.EnglishScore);
        Assert.Equal(1480m, p.SatScore);
        Assert.Equal(32m, p.ActScore);
        Assert.Equal(2, p.ExtracurricularTier);
        Assert.Equal("tier_35k_50k", p.BudgetTier);
    }

    [Fact]
    public void GroupsChosenButNoProfile_ProfileSummaryNull()
    {
        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { SharedFields = [.. LeadSharedFields.All] }, "Trung Tâm Test", "Nguyen Van A", "a@example.com", null);

        Assert.Equal("Nguyen Van A", dto.StudentName);
        Assert.Null(dto.ProfileSummary);
    }
}
