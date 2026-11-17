using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

public class ChatReviewTests
{
    private static Guid Id(int n) => Guid.Parse($"00000000-0000-0000-0000-{n:D12}");
    private static DateTime Day(int day, int hour = 12) => new(2026, 11, day, hour, 0, 0, DateTimeKind.Utc);

    // Meta của tin học sinh, có đoạn chữ và thực thể thật của học sinh: bộ đọc không được để chúng lọt ra
    private const string SecretSpan = "Marine Biology ở Hawaii học bổng toàn phần";
    private static readonly string QuestionMeta =
        "{\"v\":1,\"slm\":{\"modelIntents\":[{\"type\":\"knowledge\",\"span\":\"" + SecretSpan + "\",\"entities\":{\"gpa\":\"em GPA 3.6\"}}]," +
        "\"intents\":[\"knowledge\"],\"outOfScope\":null,\"confidence\":0.41,\"fallback\":false}}";


    private static ChatMessage Msg(int n, int session, string role, DateTime at, string? outcome = "no_answer", string? review = "open",
        int? replyTo = null, string? meta = null) =>
        new()
        {
            Id = Id(200 + n), ChatSessionId = Id(100 + session), Role = role, CreatedAt = at, Outcome = outcome, ReviewStatus = review,
            ReplyToMessageId = replyTo is { } r ? Id(200 + r) : null, MetaJson = meta, Content = "nội dung riêng tư",
        };

    // ---- bộ đọc nhãn đầy đủ (chỉ dùng cho chi tiết)

    [Fact]
    public void ReadModelDetail_KeepsTheStudentsWordsAndEntities_ForTheDetailView()
    {
        var model = ChatReviewMetaReader.ReadModelDetail(QuestionMeta)!;

        Assert.Equal((0.41, false), (model.Confidence, model.Fallback));
        var intent = Assert.Single(model.ModelIntents);
        Assert.Equal(("knowledge", SecretSpan), (intent.Type, intent.Span));
        Assert.Equal("em GPA 3.6", intent.Entities!.Value.GetProperty("gpa").GetString());
        Assert.Equal(["knowledge"], model.IntentTypes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("hỏng")]
    [InlineData("{\"v\":1}")]
    public void ReadModelDetail_WithMissingOrBrokenMeta_IsNull(string? meta) => Assert.Null(ChatReviewMetaReader.ReadModelDetail(meta));

    [Fact]
    public void ReadModelDetail_SkipsMalformedIntents_AndToleratesMissingParts()
    {
        const string meta = "{\"slm\":{\"modelIntents\":[1,{\"span\":\"thiếu type\"},{\"type\":\"scenario\"}],\"fallback\":true}}";

        var model = ChatReviewMetaReader.ReadModelDetail(meta)!;

        var intent = Assert.Single(model.ModelIntents);
        Assert.Equal(("scenario", ""), (intent.Type, intent.Span));   // thiếu span thì để trống
        Assert.Null(intent.Entities);
        Assert.True(model.Fallback);
    }

    [Fact]
    public void QuestionDetail_FindsTheQuestionWithItsMeta()
    {
        var messages = new[] { Msg(1, 1, "user", Day(2), outcome: null, review: null, meta: QuestionMeta) }.AsQueryable();

        var row = Assert.Single(ChatReviewAggregator.QuestionDetail(messages, Id(201)).ToList());

        Assert.Equal((Id(201), QuestionMeta), (row.MessageId, row.MetaJson));
    }

    [Fact]
    public void ContextBefore_IsThePreviousUserAndAssistantMessagesOfTheSameSession_NearestFirst_Limited()
    {
        var messages = new[]
        {
            Msg(1, 1, "user", Day(2, 8), outcome: null, review: null),
            Msg(2, 1, "assistant", Day(2, 9), outcome: "answered", review: null),
            Msg(3, 1, "system", Day(2, 10), outcome: "failed"),                             // dòng lỗi: không tính là ngữ cảnh
            Msg(4, 1, "user", Day(2, 11), outcome: null, review: null),
            Msg(5, 2, "user", Day(2, 11), outcome: null, review: null),                     // phiên khác
            Msg(6, 1, "user", Day(2, 13), outcome: null, review: null),                     // sau câu hỏi
            Msg(7, 1, "user", Day(2, 12), outcome: null, review: null),                     // chính câu hỏi: loại ra
        }.AsQueryable();

        var all = ChatReviewAggregator.ContextBefore(messages, Id(101), Day(2, 12), Id(207), 10).ToList();
        var limited = ChatReviewAggregator.ContextBefore(messages, Id(101), Day(2, 12), Id(207), 2).ToList();

        Assert.Equal([Id(204), Id(202), Id(201)], all.Select(r => r.MessageId));
        Assert.Equal([Id(204), Id(202)], limited.Select(r => r.MessageId));
    }

    [Fact]
    public void TheDetailQueries_TranslateToSql()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        var question = ChatReviewAggregator.QuestionDetail(db.ChatMessages, Id(201)).ToQueryString();
        var context = ChatReviewAggregator.ContextBefore(db.ChatMessages, Id(101), Day(2), Id(207), 6).ToQueryString();

        Assert.Contains("content", question);
        Assert.Contains("LIMIT", context);
        Assert.Contains("ORDER BY", context);
    }
}
