using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

// [UC-103] Bảng policy_documents: DB tự chặn các trạng thái sai (hai bản đang hiệu lực, hai bản nháp, trùng số phiên bản...).
public class PolicyDocumentModelTests
{
    private static AppDbContext SchemaOnlyDb() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=localhost;Database=usas_schema_check")
        .UseSnakeCaseNamingConvention()
        .Options);

    [Fact]
    public void EachKind_HasAtMostOnePublishedAndOneDraft_AndVersionsDoNotRepeat()
    {
        using var db = SchemaOnlyDb();
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PolicyDocument))!;
        Assert.Equal("policy_documents", entity.GetTableName());

        var indexes = entity.GetIndexes().ToList();
        var published = indexes.Single(i => i.GetDatabaseName() == "ux_policy_documents_one_published");
        var draft = indexes.Single(i => i.GetDatabaseName() == "ux_policy_documents_one_draft");
        Assert.True(published.IsUnique);
        Assert.Equal("status = 'published'", published.GetFilter());
        Assert.True(draft.IsUnique);
        Assert.Equal("status = 'draft'", draft.GetFilter());

        var version = indexes.Single(i => i.Properties.Select(p => p.Name).SequenceEqual(["Kind", "Version"]));
        Assert.True(version.IsUnique);
        Assert.Equal("version IS NOT NULL", version.GetFilter());
    }

    [Fact]
    public void TheDatabase_RejectsUnknownKindsAndStatuses_AndInconsistentShapes()
    {
        using var db = SchemaOnlyDb();
        var checks = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(PolicyDocument))!.GetCheckConstraints().ToDictionary(c => c.Name!, c => c.Sql);

        Assert.Contains(PolicyKinds.ForumRules, checks["ck_policy_documents_kind"]);
        Assert.Contains(PolicyStatuses.Retired, checks["ck_policy_documents_status"]);
        // Nháp chưa có số phiên bản; đã công bố thì phải có số phiên bản, thời điểm và tóm tắt thay đổi
        Assert.Contains("version IS NULL", checks["ck_policy_documents_draft_shape"]);
        Assert.Contains("change_summary IS NOT NULL", checks["ck_policy_documents_published_shape"]);
        Assert.Contains("retired_at IS NOT NULL", checks["ck_policy_documents_retired_shape"]);
    }

    [Fact]
    public void TheMigration_CreatesTheTable_AndSeedsTheThreeExistingDocuments()
    {
        using var db = SchemaOnlyDb();
        var sql = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>()
            .GenerateScript("20261122102238_AddSensitivityRuns", "20261122143015_AddPolicyDocuments");

        Assert.Contains("CREATE TABLE app.policy_documents", sql);
        Assert.Contains("'terms_of_use', '2026-11', 'published'", sql);
        Assert.Contains("'privacy_policy', '2026-11', 'published'", sql);
        // Giữ đúng phiên bản các bản ghi đồng ý của phụ huynh đang dùng
        Assert.Contains($"'child_data_consent', '{ConsentPurposes.ChildDataPolicyVersion}', 'published'", sql);
        // Nội quy diễn đàn chưa có văn bản nên không được nạp sẵn
        var start = sql.IndexOf("INSERT INTO app.policy_documents", StringComparison.Ordinal);
        var insert = sql[start..sql.IndexOf(';', start)];
        Assert.DoesNotContain("forum_rules", insert);
    }
}
