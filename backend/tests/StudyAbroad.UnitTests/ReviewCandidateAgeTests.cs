using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// UC-147: câu ứng viên không được thuộc học sinh dưới 16 tuổi chưa có đồng ý của phụ huynh (cùng luật với mẫu hội thoại UC-90).
public class ReviewCandidateAgeTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime At(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    // Sinh 01/01/2011: ngày 5/11/2026 mới 15 tuổi (tròn 16 vào 01/01/2027)
    private static readonly DateOnly Minor = new(2011, 1, 1);
    private static readonly DateOnly Adult = new(2000, 1, 1);

    private static ReviewCandidateOwner Row(int n, int owner, DateOnly? birth, bool parentOperated = false, DateTime? at = null) =>
        new(Id(n), "{\"skills\":[\"knowledge\"]}", at ?? At(5), Id(owner), birth, parentOperated);

    private static IReadOnlyList<ReviewCandidate> Allowed(
        IEnumerable<ReviewCandidateOwner> rows, Dictionary<int, string?[]>? consents = null) =>
        ReviewCandidateQuery.AllowedToRead(rows, owner => consents?.FirstOrDefault(c => Id(c.Key) == owner).Value ?? []);

    [Fact]
    public void Adults_AndAccountsWithoutDateOfBirth_AreSampledAsBefore()
    {
        var result = Allowed([Row(1, 1, Adult), Row(2, 2, null)]);

        Assert.Equal([Id(1), Id(2)], result.Select(c => c.MessageId));
    }

    [Fact]
    public void Minor_WithoutParentConsent_IsNeverSampled()
    {
        Assert.Empty(Allowed([Row(1, 1, Minor)]));
    }

    [Fact]
    public void Minor_WithConsentToAnOlderPolicyThatDoesNotMentionQualityReview_IsNotSampled()
    {
        var consents = new Dictionary<int, string?[]> { [1] = [ConsentPurposes.ChildDataPolicyVersion, null] };   // "2026-10" < mốc "2026-11"

        Assert.Empty(Allowed([Row(1, 1, Minor)], consents));
    }

    [Fact]
    public void Minor_WithConsentToTheQualityReviewPolicy_IsSampled()
    {
        var consents = new Dictionary<int, string?[]> { [1] = [ConsentPurposes.QualityReviewMinPolicyVersion] };

        Assert.Single(Allowed([Row(1, 1, Minor)], consents));
    }

    [Fact]
    public void AccountsOperatedByAParent_AreNotSubjectToTheUnder16Rule()
    {
        // Người thao tác là phụ huynh (users.status = managed), cùng ngoại lệ với mẫu hội thoại
        Assert.Single(Allowed([Row(1, 1, Minor, parentOperated: true)]));
    }

    [Fact]
    public void AgeIsCountedOnTheDayOfTheTurn_NotToday()
    {
        // Sinh 01/01/2011, lượt chat ngày 05/11/2026 (15 tuổi); hôm nay đã quá 01/01/2027 cũng không làm hội thoại cũ tự mở ra
        Assert.Empty(Allowed([Row(1, 1, Minor, at: At(5))]));
        // Cùng người, lượt chat sau khi tròn 16 tuổi thì được
        Assert.Single(Allowed([Row(2, 1, Minor, at: new DateTime(2027, 1, 2, 12, 0, 0, DateTimeKind.Utc))]));
    }

    [Fact]
    public void MixedOwners_KeepOnlyTheAllowedOnes_InTheSameOrder_WithTheirMeta()
    {
        var consents = new Dictionary<int, string?[]> { [3] = [ConsentPurposes.QualityReviewMinPolicyVersion] };
        var rows = new[] { Row(1, 1, Minor), Row(2, 2, Adult), Row(3, 3, Minor), Row(4, 1, Minor) };

        var result = Allowed(rows, consents);

        Assert.Equal([Id(2), Id(3)], result.Select(c => c.MessageId));
        Assert.All(result, c => Assert.Equal("{\"skills\":[\"knowledge\"]}", c.ReplyMetaJson));
    }

    // ---- truy vấn lấy chủ phiên

    private static ChatMessage Reply(int n, int session, DateTime at, string? meta = null) =>
        new()
        {
            Id = Id(n), ChatSessionId = Id(session), Role = "assistant", Outcome = ChatOutcomes.Answered, CreatedAt = at,
            ReplyToMessageId = Id(900 + n), MetaJson = meta, Content = "x",
        };

    [Fact]
    public void WithOwner_JoinsTheSessionOwner_DropsDeletedSessions_AndKeepsTheOrderOfCandidates()
    {
        var messages = new[]
        {
            Reply(2, 11, At(3, 10)),
            Reply(1, 10, At(3, 9), "{\"skills\":[\"scenario\"]}"),
            Reply(3, 12, At(3, 11)),   // cuộc trò chuyện đã xóa
            Reply(4, 13, At(3, 12)),   // chủ phiên không còn tài khoản
        }.AsQueryable();
        var sessions = new[]
        {
            new ChatSession { Id = Id(10), UserId = Id(1) },
            new ChatSession { Id = Id(11), UserId = Id(2) },
            new ChatSession { Id = Id(12), UserId = Id(1), DeletedAt = At(4) },
            new ChatSession { Id = Id(13), UserId = Id(99) },
        }.AsQueryable();
        var users = new[]
        {
            new User { Id = Id(1), DateOfBirth = Minor },
            new User { Id = Id(2), DateOfBirth = Adult, Status = UserStatuses.Managed },
        }.AsQueryable();

        var rows = ReviewCandidateQuery.WithOwner(messages, null, null, sessions, users).ToList();

        Assert.Equal([Id(1), Id(2), Id(4)], rows.Select(r => r.MessageId));   // cũ trước; tin của phiên đã xóa không có
        Assert.Equal(Minor, rows[0].OwnerDateOfBirth);
        Assert.False(rows[0].OwnerIsParentOperated);
        Assert.Equal("{\"skills\":[\"scenario\"]}", rows[0].ReplyMetaJson);
        Assert.True(rows[1].OwnerIsParentOperated);
        Assert.Null(rows[2].OwnerDateOfBirth);   // tài khoản đã xóa: không có dòng users
        Assert.Equal(Id(99), rows[2].OwnerId);
    }

    [Fact]
    public void WithOwner_TranslatesToSql_AndNeverSelectsTheChatText()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var sql = ReviewCandidateQuery.WithOwner(db.ChatMessages, null, null, db.ChatSessions, db.Users).ToQueryString();

        Assert.Contains("date_of_birth", sql);
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.DoesNotContain("content", sql);   // bước chọn mẫu không đọc cột nội dung chat nào
    }
}
