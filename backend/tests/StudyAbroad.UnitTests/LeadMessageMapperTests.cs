using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;
using Xunit;

namespace StudyAbroad.UnitTests;

public class LeadMessageMapperTests
{
    [Theory]
    [InlineData(UserRoles.Center, true)]
    [InlineData(UserRoles.Moderator, true)]
    [InlineData(UserRoles.Admin, true)]
    [InlineData(UserRoles.Student, false)]
    [InlineData(UserRoles.Parent, false)]
    [InlineData(null, false)]
    public void IsStaffOrCenterRole_ClassifiesRolesCorrectly(string? role, bool expected)
    {
        Assert.Equal(expected, LeadMessageMapper.IsStaffOrCenterRole(role));
    }

    [Theory]
    [InlineData(UserRoles.Center, "Staff A", false, "Du Học Á Âu", "Du Học Á Âu")]
    [InlineData(UserRoles.Center, "Staff A", false, "  ", LeadMessageMapper.DefaultCenterName)]
    [InlineData(UserRoles.Moderator, "Mod A", true, "Du Học Á Âu", "Kiểm duyệt viên")]
    [InlineData(UserRoles.Admin, "Admin A", true, "Du Học Á Âu", "Quản trị viên")]
    [InlineData(UserRoles.Student, "Nguyễn Minh", true, "Du Học Á Âu", "Nguyễn Minh")]
    [InlineData(UserRoles.Student, "Nguyễn Minh", false, "Du Học Á Âu", LeadMessageMapper.AnonymousStudentName)]
    [InlineData(UserRoles.Parent, "Phụ huynh Lan", false, "Du Học Á Âu", LeadMessageMapper.AnonymousStudentName)]
    [InlineData(UserRoles.Parent, "   ", true, "Du Học Á Âu", LeadMessageMapper.AnonymousStudentName)]
    public void ResolveSenderDisplayName_RespectsRoleAndSharedProfile(
        string role,
        string? fullName,
        bool sharedProfile,
        string centerName,
        string expectedDisplayName)
    {
        var result = LeadMessageMapper.ResolveSenderDisplayName(role, fullName, sharedProfile, centerName);
        Assert.Equal(expectedDisplayName, result);
    }

    [Fact]
    public void QueryThreadRows_JoinsUsersOrdersChronologicallyAndFallsBackForMissingUser()
    {
        var targetLeadId = Guid.NewGuid();
        var otherLeadId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var centerUserId = Guid.NewGuid();
        var deletedUserId = Guid.NewGuid();
        var baseTime = new DateTime(2026, 11, 11, 9, 0, 0, DateTimeKind.Utc);

        var users = new[]
        {
            new User { Id = studentId, FullName = "Trần Gia Bảo", Role = UserRoles.Student, Email = "bao@example.com", PasswordHash = "x" },
            new User { Id = centerUserId, FullName = "Tư vấn viên 1", Role = UserRoles.Center, Email = "center@example.com", PasswordHash = "x" },
        }.AsQueryable();

        var messages = new[]
        {
            new LeadMessage { Id = Guid.NewGuid(), LeadId = targetLeadId, SenderUserId = centerUserId, Content = "Trả lời sau", CreatedAt = baseTime.AddMinutes(5), Status = "visible" },
            new LeadMessage { Id = Guid.NewGuid(), LeadId = otherLeadId, SenderUserId = studentId, Content = "Lead khác", CreatedAt = baseTime, Status = "visible" },
            new LeadMessage { Id = Guid.NewGuid(), LeadId = targetLeadId, SenderUserId = deletedUserId, Content = "Tài khoản không còn", CreatedAt = baseTime.AddMinutes(2), Status = "visible" },
            new LeadMessage { Id = Guid.NewGuid(), LeadId = targetLeadId, SenderUserId = studentId, Content = "Hỏi đầu tiên", CreatedAt = baseTime, Status = "visible" },
        }.AsQueryable();

        var rows = LeadMessageMapper.QueryThreadRows(messages, users, targetLeadId).ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("Hỏi đầu tiên", rows[0].Content);
        Assert.Equal(UserRoles.Student, rows[0].SenderRole);
        Assert.Equal("Trần Gia Bảo", rows[0].SenderFullName);

        Assert.Equal("Tài khoản không còn", rows[1].Content);
        Assert.Equal(UserRoles.Student, rows[1].SenderRole);
        Assert.Null(rows[1].SenderFullName);

        Assert.Equal("Trả lời sau", rows[2].Content);
        Assert.Equal(UserRoles.Center, rows[2].SenderRole);
    }

    [Fact]
    public void ToDto_MasksRemovedContentAndFlagReason_EvenWhenFlagged()
    {
        var row = new RawLeadMessageRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Số điện thoại 0912345678 vi phạm",
            true,
            "Chứa số điện thoại liên hệ",
            "removed",
            DateTime.UtcNow,
            UserRoles.Center,
            "Staff");

        var dto = LeadMessageMapper.ToDto(row, sharedProfile: false, centerName: "Trung Tâm A");

        Assert.Equal("Trung Tâm A", dto.SenderName);
        Assert.True(dto.IsFromCenter);
        Assert.True(dto.IsFlagged);
        Assert.Equal(LeadMessageMapper.RemovedContentPlaceholder, dto.Content);
        Assert.Null(dto.FlagReason);
    }

    [Fact]
    public void AppDbContext_LeadMessageConfiguration_MatchesExpectedColumnsAndConstraints()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_lead_message_check")
            .UseSnakeCaseNamingConvention()
            .Options;

        using var db = new AppDbContext(options);
        var entityType = db.Model.FindEntityType(typeof(LeadMessage));
        Assert.NotNull(entityType);

        Assert.Equal("lead_messages", entityType!.GetTableName());
        Assert.Equal(4000, entityType.FindProperty(nameof(LeadMessage.Content))!.GetMaxLength());
        Assert.Equal(500, entityType.FindProperty(nameof(LeadMessage.FlagReason))!.GetMaxLength());
        Assert.Equal(32, entityType.FindProperty(nameof(LeadMessage.Status))!.GetMaxLength());
        Assert.Equal("visible", entityType.FindProperty(nameof(LeadMessage.Status))!.GetDefaultValue());

        var hasLeadIdIndex = entityType.GetIndexes()
            .Any(i => i.Properties.Select(p => p.Name).SequenceEqual([nameof(LeadMessage.LeadId)]));
        Assert.True(hasLeadIdIndex);
    }
}
