using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-110 bước 1] Mail hỏi phụ huynh có bản xem trước Tóm tắt hồ sơ: chỉ nhóm con đã chọn, kèm giá trị thật.</summary>
public class LeadParentPreviewTests
{
    private static readonly StudentProfile Profile = new()
    {
        OverallGpa = 3.8m,
        EducationSystem = EducationSystems.Specialized,
        CurrentGrade = "Lớp 11",
        TargetLevel = StudyLevels.Undergraduate,
        IntendedMajor = "Computer Science",
        ExtracurricularTier = 2,
        BudgetTier = "tier_35k_50k",
    };

    private static readonly IReadOnlyList<ProfileCertification> Certifications =
    [
        new() { Category = "english", TestType = "ielts", OverallScore = 7.5m, TestDate = new DateOnly(2026, 3, 1) },
        new() { Category = "standardized", TestType = "sat", OverallScore = 1480m },
    ];

    private static IReadOnlyList<LeadSharedPreviewGroup> Preview(string? message, params string[] fields)
    {
        var lead = new Lead { UserId = Guid.NewGuid(), SharedFields = [.. fields], ContactPhone = "0912345678", Message = message };
        var view = CenterLeadMapper.ToCenterDto(lead, "Trung Tâm Test", "Nguyen Van A", "a@example.com", Profile, Certifications);
        return LeadParentConfirm.BuildPreview(view, lead.SharedFields);
    }

    private static string Value(IReadOnlyList<LeadSharedPreviewGroup> groups, string label) =>
        groups.SelectMany(g => g.Items).Single(i => i.Label == label).Value;

    [Fact]
    public void ContactAndAcademic_OnlyThoseGroupsWithRealValues()
    {
        var groups = Preview(null, LeadSharedFields.Contact, LeadSharedFields.Academic);

        Assert.Equal(["Thông tin liên hệ", "Học lực"], groups.Select(g => g.Title));
        Assert.Equal("Nguyen Van A", Value(groups, "Họ tên"));
        Assert.Equal("a@example.com", Value(groups, "Email"));
        Assert.Equal("0912345678", Value(groups, "Số điện thoại"));
        Assert.Equal("3.8 / 4.0", Value(groups, "GPA tích lũy"));
        Assert.Equal("Trường chuyên / năng khiếu", Value(groups, "Hệ học"));
        Assert.Equal("Lớp 11", Value(groups, "Lớp hiện tại"));
        Assert.DoesNotContain(groups.SelectMany(g => g.Items), i => i.Label is "SAT" or "Ngành dự định" or "Mức ngân sách");
    }

    [Fact]
    public void TestsGoalsBudget_NoContactData()
    {
        var groups = Preview(null, LeadSharedFields.Tests, LeadSharedFields.Goals, LeadSharedFields.Budget);

        Assert.Equal(["Điểm thi", "Mục tiêu", "Ngân sách và ngoại khóa"], groups.Select(g => g.Title));
        Assert.Equal("IELTS: 7.5", Value(groups, "Tiếng Anh"));
        Assert.Equal("1480", Value(groups, "SAT"));
        Assert.Equal("Chưa có", Value(groups, "ACT"));
        Assert.Equal("Đại học", Value(groups, "Bậc học"));
        Assert.Equal("Computer Science", Value(groups, "Ngành dự định"));
        Assert.Equal("$35,000 - $50,000 / năm", Value(groups, "Mức ngân sách"));
        Assert.Equal("Bậc 2", Value(groups, "Mức hoạt động ngoại khóa"));
        Assert.DoesNotContain(groups.SelectMany(g => g.Items), i => i.Label is "Họ tên" or "Email" or "GPA tích lũy");
    }

    [Fact]
    public void Message_AddedAsLastGroup()
    {
        var groups = Preview("  Em muốn hỏi học bổng  ", LeadSharedFields.Goals);

        Assert.Equal("Lời nhắn gửi trung tâm", groups[^1].Title);
        Assert.Equal("Em muốn hỏi học bổng", Value(groups, "Lời nhắn"));
    }

    [Fact]
    public void ContactWithoutPhone_SaysNotEntered()
    {
        var lead = new Lead { UserId = Guid.NewGuid(), SharedFields = [LeadSharedFields.Contact] };
        var view = CenterLeadMapper.ToCenterDto(lead, "Trung Tâm Test", "Nguyen Van A", "a@example.com", null);

        var groups = LeadParentConfirm.BuildPreview(view, lead.SharedFields);

        Assert.Equal("Không nhập", Value(groups, "Số điện thoại"));
    }
}
