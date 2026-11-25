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
        TargetLevel = "bachelor",
        IntendedMajor = "Computer Science",
        ExtracurricularTier = 2,
        BudgetTier = "tier_35k_50k",
    };

    private static readonly IReadOnlyList<ProfileCertification> Certifications =
    [
        new() { Category = "english", TestType = "ielts", OverallScore = 7.5m, TestDate = new DateOnly(2026, 3, 1) },
        new() { Category = "standardized", TestType = "sat", OverallScore = 1480m },
        new() { Category = "standardized", TestType = "act", OverallScore = 32m },
    ];

    private static CenterReceivedLeadDto Map(params string[] fields) =>
        CenterLeadMapper.ToCenterDto(
            new Lead { UserId = Guid.NewGuid(), SharedFields = [.. fields], ContactPhone = "0912345678" },
            "Trung Tâm Test",
            "Nguyen Van A",
            "a@example.com",
            Profile,
            Certifications);

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
    public void Withdrawn_HidesAllStudentData()
    {
        var dto = CenterLeadMapper.ToCenterDto(
            new Lead
            {
                UserId = Guid.NewGuid(),
                SharedFields = [LeadSharedFields.Contact, LeadSharedFields.Academic, LeadSharedFields.Goals],
                SharedProfile = true,
                ContactPhone = "0912345678",
                Message = "Em muốn tư vấn",
                StudyLevel = "bachelor",
                Status = LeadStatuses.Withdrawn,
            },
            "Trung Tâm Test", "Nguyen Van A", "a@example.com", Profile, Certifications);

        Assert.Equal(LeadStatuses.Withdrawn, dto.Status);
        Assert.Null(dto.StudentName);
        Assert.Null(dto.StudentEmail);
        Assert.Null(dto.ContactPhone);
        Assert.Null(dto.Message);
        Assert.Null(dto.StudyLevel);
        Assert.Null(dto.ProfileSummary);
        Assert.False(dto.SharedProfile);
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
    public void Tests_TargetScoresGoToPlannedTests_NotToScores()
    {
        // Chưa thi tiếng Anh, đã thi SAT 1400 và dự định thi lại 1500, dự định IELTS 7.0
        var profile = new StudentProfile();
        IReadOnlyList<ProfileCertification> certs =
        [
            new() { Category = "standardized", TestType = "sat", OverallScore = 1400m, TestDate = new DateOnly(2026, 3, 1), SortOrder = 0 },
            new() { Category = "standardized", TestType = "sat", OverallScore = 1500m, TestDate = new DateOnly(2027, 3, 1), IsTarget = true, SortOrder = 1 },
            new() { Category = "english", TestType = "ielts", OverallScore = 7m, TestDate = new DateOnly(2027, 1, 1), IsTarget = true, SortOrder = 2 },
        ];

        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { UserId = Guid.NewGuid(), SharedFields = [LeadSharedFields.Tests] }, "Trung Tâm Test", "Nguyen Van A", "a@example.com", profile, certs);

        var p = Assert.IsType<SharedProfileSummaryDto>(dto.ProfileSummary);
        Assert.Null(p.EnglishScore);
        Assert.Null(p.EnglishTestType);
        Assert.Equal(1400m, p.SatScore);
        Assert.Null(p.ActScore);
        Assert.Collection(p.PlannedTests!,
            sat => { Assert.Equal("sat", sat.TestType); Assert.Equal(1500m, sat.TargetScore); Assert.Equal(new DateOnly(2027, 3, 1), sat.PlannedDate); },
            ielts => { Assert.Equal("ielts", ielts.TestType); Assert.Equal(7m, ielts.TargetScore); });
    }

    [Fact]
    public void TestsNotShared_NoPlannedTests()
    {
        IReadOnlyList<ProfileCertification> certs = [new() { TestType = "sat", OverallScore = 1500m, IsTarget = true }];

        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { UserId = Guid.NewGuid(), SharedFields = [LeadSharedFields.Goals] }, "Trung Tâm Test", null, null, Profile, certs);

        Assert.Null(dto.ProfileSummary!.PlannedTests);
        Assert.Null(dto.ProfileSummary.SatScore);
    }

    [Fact]
    public void Tests_MainEnglishIsLatestTakenTest_WithItsBestScore()
    {
        IReadOnlyList<ProfileCertification> certs =
        [
            new() { Category = "english", TestType = "ielts", OverallScore = 7m, TestDate = new DateOnly(2025, 6, 1) },
            new() { Category = "english", TestType = "toefl", OverallScore = 95m, TestDate = new DateOnly(2026, 1, 1) },
            new() { Category = "english", TestType = "toefl", OverallScore = 100m, TestDate = new DateOnly(2025, 9, 1) },
            // dự định thi sau không tính là bài chính
            new() { Category = "english", TestType = "ielts", OverallScore = 8m, TestDate = new DateOnly(2027, 1, 1), IsTarget = true },
        ];

        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { UserId = Guid.NewGuid(), SharedFields = [LeadSharedFields.Tests] }, "Trung Tâm Test", null, null, Profile, certs);

        var p = Assert.IsType<SharedProfileSummaryDto>(dto.ProfileSummary);
        Assert.Equal("toefl", p.EnglishTestType);
        Assert.Equal(100m, p.EnglishScore);
    }

    [Fact]
    public void GroupsChosenButNoProfile_ProfileSummaryNull()
    {
        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { UserId = Guid.NewGuid(), SharedFields = [.. LeadSharedFields.All] }, "Trung Tâm Test", "Nguyen Van A", "a@example.com", null);

        Assert.Equal("Nguyen Van A", dto.StudentName);
        Assert.Null(dto.ProfileSummary);
    }

    [Fact]
    public void ToCenterDto_LeadOlderThan90Days_HidesSharedData()
    {
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), StudyCenterId = Guid.NewGuid(), Status = "accepted", ContactPhone = "0912345678",
            Message = "Em hỏi", SharedProfile = true, SharedFields = [LeadSharedFields.Contact],
            CreatedAt = DateTime.UtcNow.AddDays(-(CenterLeadMapper.DataVisibleDays + 1)),
        };

        var dto = CenterLeadMapper.ToCenterDto(lead, "TT", "Nguyễn Văn A", "a@example.com", null);

        Assert.True(dto.DataHidden);
        Assert.Null(dto.StudentName);
        Assert.Null(dto.StudentEmail);
        Assert.Null(dto.ContactPhone);
        Assert.Null(dto.Message);
        Assert.Equal("accepted", dto.Status);

        lead.CreatedAt = DateTime.UtcNow.AddDays(-(CenterLeadMapper.DataVisibleDays - 1));
        var visible = CenterLeadMapper.ToCenterDto(lead, "TT", "Nguyễn Văn A", "a@example.com", null);
        Assert.False(visible.DataHidden);
        Assert.Equal("Nguyễn Văn A", visible.StudentName);
    }

    [Fact]
    public void ToCenterDto_SenderDeleted_HidesSharedData()
    {
        // [UC-75 lớp 1b] Người gửi đã bị xóa hẳn: lead chỉ còn để giữ tin nhắn
        var dto = CenterLeadMapper.ToCenterDto(
            new Lead { UserId = null, SharedFields = [.. LeadSharedFields.All], ContactPhone = "0912345678" },
            "Trung Tâm Test", null, null, Profile, Certifications);

        Assert.Null(dto.StudentName);
        Assert.Null(dto.ContactPhone);
        Assert.Null(dto.ProfileSummary);
    }
}
