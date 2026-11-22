using System.Text.Json;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [UC-103] Soạn nháp, sửa nháp, công bố, bỏ nháp, lịch sử. Bản đã công bố không bao giờ sửa được.
public class PolicyServiceTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly PolicyActor Actor = new(Admin, "10.0.0.5", "unit-test");
    private const string Kind = PolicyKinds.TermsOfUse;

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    // Kho giả mô phỏng các ràng buộc của DB: mỗi loại tối đa một bản đang hiệu lực, một bản nháp, số phiên bản không trùng.
    private sealed class FakeDocuments : IPolicyDocumentRepository
    {
        public List<PolicyDocument> Rows { get; } = [];
        public int Saves { get; private set; }

        public Task<PolicyDocument?> FindAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Rows.FirstOrDefault(d => d.Id == id));
        public Task<PolicyDocument?> FindPublishedAsync(string kind, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(d => d.Kind == kind && d.Status == PolicyStatuses.Published));
        public Task<PolicyDocument?> FindDraftAsync(string kind, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(d => d.Kind == kind && d.Status == PolicyStatuses.Draft));
        public Task<IReadOnlyList<PolicyDocument>> ListAsync(string? kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PolicyDocument>>(Rows.Where(d => kind is null || d.Kind == kind).OrderByDescending(d => d.CreatedAt).ToList());
        public Task<IReadOnlyList<string>> ListVersionsAsync(string kind, string prefix, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Rows.Where(d => d.Kind == kind && d.Version != null && d.Version.StartsWith(prefix)).Select(d => d.Version!).ToList());

        public Task AddAsync(PolicyDocument document, CancellationToken ct = default)
        {
            Rows.Add(document);
            return Check(() => Rows.Remove(document));
        }

        public Task SaveAsync(CancellationToken ct = default) => Check(null);

        public Task RemoveAsync(PolicyDocument document, CancellationToken ct = default)
        {
            Rows.Remove(document);
            return Check(null);
        }

        private Task Check(Action? undo)
        {
            Saves++;
            foreach (var group in Rows.GroupBy(d => d.Kind))
            {
                var bad = group.Count(d => d.Status == PolicyStatuses.Published) > 1
                          || group.Count(d => d.Status == PolicyStatuses.Draft) > 1
                          || group.Where(d => d.Version != null).GroupBy(d => d.Version).Any(g => g.Count() > 1);
                if (!bad) continue;
                undo?.Invoke();
                throw new PolicyRuleException("policy_conflict", "DB từ chối: vi phạm chỉ mục duy nhất.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RunDirectly : IUnitOfWork
    {
        public int Runs { get; private set; }
        public Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        {
            Runs++;
            return work(ct);
        }
    }

    private static (PolicyService Service, FakeDocuments Docs, SensitivityRunServiceTests.FakeAudits Audits, FixedClock Clock, RunDirectly Uow) Setup(
        bool withPublishedTerms = true)
    {
        var docs = new FakeDocuments();
        if (withPublishedTerms)
            docs.Rows.Add(new PolicyDocument
            {
                Kind = Kind, Version = "2026-10", Status = PolicyStatuses.Published, Title = "Điều khoản sử dụng", Content = "## 1. Giới thiệu\n\nBản cũ.",
                ChangeSummary = "Bản đầu", PublishedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            });
        var audits = new SensitivityRunServiceTests.FakeAudits();
        var clock = new FixedClock(new DateTimeOffset(2026, 11, 22, 10, 0, 0, TimeSpan.Zero));
        var uow = new RunDirectly();
        return (new PolicyService(docs, audits, uow, clock), docs, audits, clock, uow);
    }

    private static UpdatePolicyDraftRequest Edit(string content = "## 1. Giới thiệu\n\nBản mới.", string? summary = "Thêm mục mới", bool reconsent = true) =>
        new("Điều khoản sử dụng", content, summary, reconsent);

    // ------------------------------------------------------------ tạo bản nháp

    [Fact]
    public async Task CreateDraft_CopiesTheWholeTextOfThePublishedVersion()
    {
        var (service, docs, audits, _, uow) = Setup();
        var published = docs.Rows.Single();

        var result = await service.CreateDraftAsync(Actor, Kind);

        Assert.True(result.Created);
        Assert.Equal(PolicyStatuses.Draft, result.Document.Status);
        Assert.Null(result.Document.Version);                       // bản nháp chưa có số phiên bản
        Assert.Equal(published.Content, result.Document.Content);   // admin chỉ sửa đúng chỗ cần đổi
        Assert.Equal(published.Title, result.Document.Title);
        Assert.Equal(published.Id, result.Document.BasedOnId);
        Assert.Equal(Admin, result.Document.CreatedByUserId);
        Assert.Equal(PolicyStatuses.Published, published.Status);   // bản đang hiệu lực không bị đụng
        Assert.Equal(1, uow.Runs);

        var entry = Assert.Single(audits.Rows);
        Assert.Equal(PolicyService.CreateDraftAction, entry.Action);
        Assert.Equal(result.Document.Id.ToString(), entry.EntityId);
        Assert.Equal("10.0.0.5", entry.IpAddress);
        Assert.DoesNotContain("Bản cũ", entry.DetailJson);          // nhật ký không chép toàn văn
    }

    [Fact]
    public async Task CreateDraft_WithNoPublishedVersion_StartsEmptyWithTheDefaultTitle()
    {
        var (service, _, _, _, _) = Setup(withPublishedTerms: false);

        var result = await service.CreateDraftAsync(Actor, PolicyKinds.ForumRules);

        Assert.Equal("Nội quy diễn đàn", result.Document.Title);
        Assert.Equal(string.Empty, result.Document.Content);
        Assert.Null(result.Document.BasedOnId);
    }

    [Fact]
    public async Task CreateDraft_WhenOneAlreadyExists_ReturnsThatDraft_AndCreatesNothingElse()
    {
        var (service, docs, audits, _, _) = Setup();
        var first = await service.CreateDraftAsync(Actor, Kind);

        var second = await service.CreateDraftAsync(Actor, Kind);

        Assert.False(second.Created);
        Assert.Equal(first.Document.Id, second.Document.Id);
        Assert.Single(docs.Rows, d => d.Status == PolicyStatuses.Draft);
        Assert.Single(audits.Rows);
    }

    [Fact]
    public async Task CreateDraft_ForEachKind_IsIndependent()
    {
        var (service, docs, _, _, _) = Setup();
        await service.CreateDraftAsync(Actor, PolicyKinds.TermsOfUse);
        await service.CreateDraftAsync(Actor, PolicyKinds.PrivacyPolicy);
        Assert.Equal(2, docs.Rows.Count(d => d.Status == PolicyStatuses.Draft));
    }

    [Fact]
    public async Task CreateDraft_WithAnUnknownKind_IsRejected()
    {
        var (service, docs, _, _, _) = Setup();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateDraftAsync(Actor, "cookie_policy"));
        Assert.Single(docs.Rows);
    }

    // ------------------------------------------------------------ sửa bản nháp

    [Fact]
    public async Task UpdateDraft_SavesTheEditedContent_AndLogsWithoutTheText()
    {
        var (service, _, audits, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;

        var saved = await service.UpdateDraftAsync(Actor, draft.Id, new("  Điều khoản mới  ", "Nội dung bí mật XYZ", "  Thêm mục 5.3  ", true));

        Assert.NotNull(saved);
        Assert.Equal("Điều khoản mới", saved!.Title);
        Assert.Equal("Thêm mục 5.3", saved.ChangeSummary);
        Assert.True(saved.RequiresReconsent);
        Assert.Equal("Nội dung bí mật XYZ", saved.Content);
        Assert.Equal(PolicyService.UpdateDraftAction, audits.Rows.Last().Action);
        Assert.DoesNotContain("bí mật XYZ", audits.Rows.Last().DetailJson);
    }

    [Fact]
    public async Task UpdateDraft_ABlankSummary_IsStoredAsNothing()
    {
        var (service, _, _, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        var saved = await service.UpdateDraftAsync(Actor, draft.Id, Edit(summary: "   "));
        Assert.Null(saved!.ChangeSummary);
    }

    [Fact]
    public async Task UpdateDraft_OnAPublishedVersion_IsRefused_AndTheTextIsUntouched()
    {
        var (service, docs, audits, _, _) = Setup();
        var published = docs.Rows.Single();
        var before = published.Content;

        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() => service.UpdateDraftAsync(Actor, published.Id, Edit("Sửa lén")));

        Assert.Equal("policy_locked", ex.Code);
        Assert.Equal(before, published.Content);
        Assert.Empty(audits.Rows);
    }

    [Fact]
    public async Task UpdateDraft_OnARetiredVersion_IsRefusedToo()
    {
        var (service, docs, _, _, _) = Setup();
        var old = docs.Rows.Single();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        await service.UpdateDraftAsync(Actor, draft.Id, Edit());
        await service.PublishAsync(Actor, draft.Id);
        Assert.Equal(PolicyStatuses.Retired, old.Status);

        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() => service.UpdateDraftAsync(Actor, old.Id, Edit("Sửa bản cũ")));
        Assert.Equal("policy_locked", ex.Code);
    }

    [Theory]
    [InlineData("", "ok", null)]
    [InlineData("   ", "ok", null)]
    public async Task UpdateDraft_WithABlankTitle_IsRejected(string title, string content, string? summary)
    {
        var (service, _, _, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(Actor, draft.Id, new(title, content, summary, false)));
    }

    [Fact]
    public async Task UpdateDraft_WithTooLongFields_IsRejected()
    {
        var (service, _, _, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(Actor, draft.Id, new(new string('a', PolicyService.MaxTitleLength + 1), "x", null, false)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(Actor, draft.Id, new("t", "x", new string('a', PolicyService.MaxChangeSummaryLength + 1), false)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateDraftAsync(Actor, draft.Id, new("t", new string('a', PolicyService.MaxContentLength + 1), null, false)));
    }

    // ------------------------------------------------------------ công bố

    [Fact]
    public async Task Publish_LocksTheNewVersion_AndRetiresTheOldOne_InOneTransaction()
    {
        var (service, docs, audits, _, uow) = Setup();
        var old = docs.Rows.Single();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        await service.UpdateDraftAsync(Actor, draft.Id, Edit(summary: "Thêm mục 5.3: đánh giá chất lượng AI", reconsent: true));
        var runsBefore = uow.Runs;

        var published = await service.PublishAsync(Actor, draft.Id);

        Assert.NotNull(published);
        Assert.Equal("2026-11", published!.Version);
        Assert.Equal(PolicyStatuses.Published, published.Status);
        Assert.Equal(Admin, published.PublishedByUserId);
        Assert.Equal(new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc), published.PublishedAt);
        Assert.True(published.RequiresReconsent);
        Assert.Equal(PolicyStatuses.Retired, old.Status);
        Assert.Equal(published.PublishedAt, old.RetiredAt);
        Assert.Equal("## 1. Giới thiệu\n\nBản cũ.", old.Content);   // bản cũ giữ nguyên làm bằng chứng
        Assert.Equal(1, docs.Rows.Count(d => d.Status == PolicyStatuses.Published));
        Assert.Equal(runsBefore + 1, uow.Runs);                      // một giao dịch duy nhất

        var entry = audits.Rows.Last();
        Assert.Equal(PolicyService.PublishAction, entry.Action);
        using var detail = JsonDocument.Parse(entry.DetailJson!);
        Assert.Equal("2026-11", detail.RootElement.GetProperty("version").GetString());
        Assert.Equal("2026-10", detail.RootElement.GetProperty("detail").GetProperty("previousVersion").GetString());
    }

    [Fact]
    public async Task Publish_TheFirstVersionOfAKind_HasNothingToRetire()
    {
        var (service, docs, _, _, _) = Setup(withPublishedTerms: false);
        var draft = (await service.CreateDraftAsync(Actor, PolicyKinds.ForumRules)).Document;
        await service.UpdateDraftAsync(Actor, draft.Id, new("Nội quy diễn đàn", "Không spam.", "Bản đầu tiên", false));

        var published = await service.PublishAsync(Actor, draft.Id);

        Assert.Equal("2026-11", published!.Version);
        Assert.Single(docs.Rows, d => d.Status == PolicyStatuses.Published);
        Assert.DoesNotContain(docs.Rows, d => d.Status == PolicyStatuses.Retired);
    }

    [Fact]
    public async Task Publish_TwiceInTheSameMonth_NumbersTheVersionsDashO2AndDashO3()
    {
        var (service, docs, _, _, _) = Setup();
        docs.Rows.Single().Version = "2026-10";
        var versions = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
            await service.UpdateDraftAsync(Actor, draft.Id, Edit($"Bản {i}", $"Lần {i}"));
            versions.Add((await service.PublishAsync(Actor, draft.Id))!.Version!);
        }
        Assert.Equal(["2026-11", "2026-11-02", "2026-11-03"], versions);
        // Thứ tự chữ cũng là thứ tự thời gian (UC-90 so sánh theo cách này)
        Assert.True(string.CompareOrdinal("2026-11-02", "2026-11") > 0);
        Assert.True(string.CompareOrdinal("2026-11-03", "2026-11-02") > 0);
        Assert.True(string.CompareOrdinal("2026-12", "2026-11-03") > 0);
        Assert.Equal(1, docs.Rows.Count(d => d.Status == PolicyStatuses.Published));
    }

    [Theory]
    [InlineData("2026-11-22", new string[0], "2026-11")]
    [InlineData("2026-11-22", new[] { "2026-10" }, "2026-11")]
    [InlineData("2026-11-22", new[] { "2026-11" }, "2026-11-02")]
    [InlineData("2026-11-22", new[] { "2026-11", "2026-11-02" }, "2026-11-03")]
    [InlineData("2026-11-22", new[] { "2026-11-02" }, "2026-11")]       // chỉ có -02 thì "2026-11" còn trống
    [InlineData("2026-12-01", new[] { "2026-11", "2026-11-02" }, "2026-12")]
    public void NextVersion_UsesTheYearAndMonth_AndNumbersRepeatsInAMonth(string day, string[] used, string expected) =>
        Assert.Equal(expected, PolicyService.NextVersion(DateTime.Parse(day, null, System.Globalization.DateTimeStyles.AdjustToUniversal), used));

    [Theory]
    [InlineData("", "Nội dung", "Tóm tắt")]
    [InlineData("Tiêu đề", "   ", "Tóm tắt")]
    [InlineData("Tiêu đề", "Nội dung", null)]
    [InlineData("Tiêu đề", "Nội dung", "   ")]
    public async Task Publish_WithoutTitleContentOrSummary_IsRejected_AndNothingChanges(string title, string content, string? summary)
    {
        var (service, docs, audits, _, _) = Setup();
        var old = docs.Rows.Single();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        var tracked = docs.Rows.Single(d => d.Id == draft.Id);
        tracked.Title = title; tracked.Content = content; tracked.ChangeSummary = summary;   // lưu thẳng, bỏ qua kiểm tra của bước sửa
        var auditsBefore = audits.Rows.Count;

        await Assert.ThrowsAsync<ArgumentException>(() => service.PublishAsync(Actor, draft.Id));

        Assert.Equal(PolicyStatuses.Draft, tracked.Status);
        Assert.Null(tracked.Version);
        Assert.Equal(PolicyStatuses.Published, old.Status);          // bản cũ vẫn hiệu lực
        Assert.Equal(auditsBefore, audits.Rows.Count);
    }

    [Fact]
    public async Task Publish_AVersionThatIsAlreadyPublished_IsRefused()
    {
        var (service, docs, _, _, _) = Setup();
        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() => service.PublishAsync(Actor, docs.Rows.Single().Id));
        Assert.Equal("policy_locked", ex.Code);
    }

    [Fact]
    public async Task Publish_IfTheAuditLogFails_TheErrorReachesTheCaller()
    {
        var (service, docs, audits, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;
        await service.UpdateDraftAsync(Actor, draft.Id, Edit());
        audits.Fail = new InvalidOperationException("DB lỗi");

        // Trong DB thật, giao dịch bị hủy nên không còn bản nào đổi trạng thái; ở đây chỉ kiểm tra lỗi không bị nuốt
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(Actor, draft.Id));
    }

    [Fact]
    public async Task TheFakeDatabase_WouldRejectPublishingInTheWrongOrder()
    {
        // Kiểm chứng chính bộ giả: nếu đặt bản mới có hiệu lực khi bản cũ còn hiệu lực thì vi phạm ràng buộc
        var (_, docs, _, _, _) = Setup();
        var second = new PolicyDocument { Kind = Kind, Version = "2026-11", Status = PolicyStatuses.Published, Title = "t", Content = "c", ChangeSummary = "s" };
        await Assert.ThrowsAsync<PolicyRuleException>(() => docs.AddAsync(second));
    }

    // ------------------------------------------------------------ bỏ nháp, xem, lịch sử

    [Fact]
    public async Task DiscardDraft_RemovesIt_AndAllowsStartingAgain()
    {
        var (service, docs, audits, _, _) = Setup();
        var draft = (await service.CreateDraftAsync(Actor, Kind)).Document;

        Assert.True(await service.DiscardDraftAsync(Actor, draft.Id));

        Assert.DoesNotContain(docs.Rows, d => d.Id == draft.Id);
        Assert.Equal(PolicyService.DiscardDraftAction, audits.Rows.Last().Action);
        Assert.True((await service.CreateDraftAsync(Actor, Kind)).Created);
    }

    [Fact]
    public async Task DiscardDraft_OnAPublishedVersion_IsRefused()
    {
        var (service, docs, _, _, _) = Setup();
        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() => service.DiscardDraftAsync(Actor, docs.Rows.Single().Id));
        Assert.Equal("policy_locked", ex.Code);
        Assert.Single(docs.Rows);
    }

    [Fact]
    public async Task UnknownIds_GiveNothing()
    {
        var (service, _, audits, _, _) = Setup();
        var id = Guid.NewGuid();
        Assert.Null(await service.GetAsync(id));
        Assert.Null(await service.UpdateDraftAsync(Actor, id, Edit()));
        Assert.Null(await service.PublishAsync(Actor, id));
        Assert.False(await service.DiscardDraftAsync(Actor, id));
        Assert.Empty(audits.Rows);
    }

    [Fact]
    public async Task List_ShowsTheHistory_WithoutTheFullText_AndFiltersByKind()
    {
        var (service, docs, _, clock, _) = Setup();
        docs.Rows.Add(new PolicyDocument { Kind = PolicyKinds.PrivacyPolicy, Version = "2026-11", Status = PolicyStatuses.Published, Title = "Bảo mật", Content = "abcde", ChangeSummary = "s" });
        clock.Now = clock.Now.AddMinutes(5);
        await service.CreateDraftAsync(Actor, Kind);

        var all = await service.ListAsync(null);
        var terms = await service.ListAsync(Kind);

        Assert.Equal(3, all.Count);
        Assert.Equal(2, terms.Count);
        Assert.All(terms, t => Assert.Equal(Kind, t.Kind));
        Assert.Contains(all, a => a.Title == "Bảo mật" && a.ContentLength == 5);
        Assert.Null(typeof(PolicyDocumentSummaryDto).GetProperty("Content"));   // danh sách không mang toàn văn
        await Assert.ThrowsAsync<ArgumentException>(() => service.ListAsync("cookie_policy"));
    }

    [Fact]
    public async Task Get_ReturnsTheFullText()
    {
        var (service, docs, _, _, _) = Setup();
        var dto = await service.GetAsync(docs.Rows.Single().Id);
        Assert.Equal("## 1. Giới thiệu\n\nBản cũ.", dto!.Content);
    }
}
