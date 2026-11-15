using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// Các bảng của đợt duyệt mẫu (tab 2): cấu trúc, ràng buộc và hành vi khi xóa. Chỉ dựng mô hình, không nối database.
public class ReviewBatchModelTests
{
    private static IEntityType Entity<T>()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        return db.Model.FindEntityType(typeof(T))!;
    }

    private static IForeignKey Fk<TDependent, TPrincipal>() =>
        Entity<TDependent>().GetForeignKeys().Single(k => k.PrincipalEntityType.ClrType == typeof(TPrincipal));

    private static bool HasUniqueIndexOn<T>(params string[] properties) =>
        Entity<T>().GetIndexes().Any(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(properties));

    [Theory]
    [InlineData(typeof(ReviewBatch), "review_batches")]
    [InlineData(typeof(ReviewBatchReviewer), "review_batch_reviewers")]
    [InlineData(typeof(ReviewBatchItem), "review_batch_items")]
    [InlineData(typeof(ReviewVerdict), "review_verdicts")]
    public void TheTables_AreInTheAppSchema_WithTheExpectedNames(Type entity, string table)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);

        var type = db.Model.FindEntityType(entity)!;

        Assert.Equal((table, "app"), (type.GetTableName(), type.GetSchema() ?? "app"));
    }

    [Fact]
    public void AMessage_CanBeInABatchOnlyOnce()
    {
        Assert.True(HasUniqueIndexOn<ReviewBatchItem>(nameof(ReviewBatchItem.ReviewBatchId), nameof(ReviewBatchItem.ChatMessageId)));
    }

    [Fact]
    public void AReviewer_CanBeAssignedToABatchOnlyOnce()
    {
        Assert.True(HasUniqueIndexOn<ReviewBatchReviewer>(nameof(ReviewBatchReviewer.ReviewBatchId), nameof(ReviewBatchReviewer.UserId)));
    }

    [Fact]
    public void AReviewer_GivesOneVerdictPerItem_SoTwoReviewersCanGradeTheSameItemIndependently()
    {
        Assert.True(HasUniqueIndexOn<ReviewVerdict>(nameof(ReviewVerdict.ReviewBatchItemId), nameof(ReviewVerdict.ReviewerUserId)));
        // Không có chỉ mục duy nhất chỉ trên câu: nếu có thì mỗi câu chỉ có một người chấm
        Assert.False(HasUniqueIndexOn<ReviewVerdict>(nameof(ReviewVerdict.ReviewBatchItemId)));
    }

    [Fact]
    public void DeletingTheChatMessage_RemovesItsBatchItem_SoErasingAStudentAlsoErasesTheirSampledAnswer()
    {
        Assert.Equal(DeleteBehavior.Cascade, Fk<ReviewBatchItem, ChatMessage>().DeleteBehavior);
    }

    [Fact]
    public void DeletingABatch_RemovesItsItemsAndReviewers_ButDeletingItsCreatorKeepsTheBatch()
    {
        Assert.Equal(DeleteBehavior.Cascade, Fk<ReviewBatchItem, ReviewBatch>().DeleteBehavior);
        Assert.Equal(DeleteBehavior.Cascade, Fk<ReviewBatchReviewer, ReviewBatch>().DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, Fk<ReviewBatch, User>().DeleteBehavior);
    }

    [Fact]
    public void DeletingAnItem_RemovesItsVerdicts()
    {
        Assert.Equal(DeleteBehavior.Cascade, Fk<ReviewVerdict, ReviewBatchItem>().DeleteBehavior);
    }

    // Ràng buộc kiểm (check) chỉ có trong mô hình lúc thiết kế, không có trong mô hình tối ưu để chạy
    private static string CheckSql(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check").UseSnakeCaseNamingConvention().Options;
        using var db = new AppDbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;
        return model.FindEntityType(typeof(ReviewVerdict))!.GetCheckConstraints().Single(c => c.Name == name).Sql;
    }

    [Fact]
    public void TheVerdictChecks_AllowExactlyTheValuesOfTheConstants()
    {
        var label = CheckSql("ck_review_verdicts_label_verdict");
        var answer = CheckSql("ck_review_verdicts_answer_verdict");
        var cause = CheckSql("ck_review_verdicts_cause");

        Assert.All(ReviewLabelVerdicts.All, v => Assert.Contains($"'{v}'", label));
        Assert.All(ReviewAnswerVerdicts.All, v => Assert.Contains($"'{v}'", answer));
        Assert.All(ReviewCauses.All, v => Assert.Contains($"'{v}'", cause));
        // Số giá trị trong câu SQL bằng số hằng: không có giá trị thừa lọt vào ràng buộc
        Assert.Equal(ReviewLabelVerdicts.All.Length, label.Count(c => c == '\'') / 2);
        Assert.Equal(ReviewAnswerVerdicts.All.Length, answer.Count(c => c == '\'') / 2);
        Assert.Equal(ReviewCauses.All.Length, cause.Count(c => c == '\'') / 2);
    }

    [Fact]
    public void ACorrectIntent_OnlyMakesSenseWhenTheLabelIsWrong_AndACauseOnlyWhenTheAnswerIsWrong()
    {
        Assert.Contains("label_verdict = 'wrong'", CheckSql("ck_review_verdicts_correct_intent"));
        Assert.Contains("answer_verdict = 'wrong'", CheckSql("ck_review_verdicts_cause_needs_wrong"));
    }

    [Fact]
    public void TheConstants_HaveNoDuplicates_AndKeepTheExpectedShape()
    {
        foreach (var all in new[] { ReviewLabelVerdicts.All, ReviewAnswerVerdicts.All, ReviewCauses.All, SlmIntentTypes.All })
            Assert.Equal(all.Length, all.Distinct().Count());

        Assert.Equal(6, SlmIntentTypes.All.Length);
        Assert.Contains("out_of_scope", SlmIntentTypes.All);
        Assert.Contains(ReviewAnswerVerdicts.Wrong, ReviewAnswerVerdicts.All);   // cause đi kèm giá trị này
    }

    [Fact]
    public void TheVerdictEntity_CarriesNoChatContent()
    {
        var names = Entity<ReviewVerdict>().GetProperties().Concat(Entity<ReviewBatchItem>().GetProperties()).Select(p => p.Name);

        Assert.DoesNotContain(names, n => n is "Content" or "Text" or "Question" or "Answer" or "Note" or "Comment");
    }
}
