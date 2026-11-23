using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// [UC-104] Xem tình trạng đồng ý và ghi đồng ý theo đúng phiên bản đã đọc.
public class PolicyAcceptanceServiceTests
{
    private static readonly Guid User = Guid.NewGuid();
    private const string Terms = PolicyKinds.TermsOfUse;
    private const string Privacy = PolicyKinds.PrivacyPolicy;

    private sealed class FakePublished : IPublishedPolicyReader
    {
        public Dictionary<string, List<PublishedPolicyHeader>> ByKind { get; } = [];
        public FakePublished Add(string kind, string version, bool required = false, string? summary = null)
        {
            if (!ByKind.TryGetValue(kind, out var list)) ByKind[kind] = list = [];
            list.Add(new PublishedPolicyHeader(version, required, $"Tiêu đề {kind}", summary));
            return this;
        }
        public Task<IReadOnlyList<PublishedPolicyHeader>> ListPublishedAsync(string kind, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PublishedPolicyHeader>>(ByKind.TryGetValue(kind, out var l) ? l.ToList() : []);
    }

    private sealed class FakeConsents : IConsentRepository
    {
        public List<Consent> Rows { get; } = [];
        private int _clock;
        public Task<Consent?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default) =>
            Task.FromResult(Rows.Where(c => c.UserId == userId && c.Purpose == purpose).OrderByDescending(c => c.CreatedAt).FirstOrDefault());
        public Task<IReadOnlyList<Consent>> ListAsync(Guid userId, IReadOnlyCollection<string> purposes, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Consent>>(
                Rows.Where(c => c.UserId == userId && purposes.Contains(c.Purpose)).OrderByDescending(c => c.CreatedAt).ToList());
        public Task AddAsync(Consent consent, CancellationToken ct = default)
        {
            consent.CreatedAt = new DateTime(2026, 11, 23, 0, 0, 0, DateTimeKind.Utc).AddSeconds(_clock++);   // mỗi dòng sau muộn hơn dòng trước
            Rows.Add(consent);
            return Task.CompletedTask;
        }
    }

    private readonly FakePublished _published = new();
    private readonly FakeConsents _consents = new();
    private readonly FakeUnitOfWork _uow = new();

    private PolicyAcceptanceService Create() => new(_published, _consents, _uow);

    private static AcceptPoliciesRequest Req(params (string Kind, string Version)[] items) =>
        new(items.Select(i => new AcceptPolicyItem(i.Kind, i.Version)).ToList());

    private PolicyAcceptanceService WithBoth(string version = "2026-11")
    {
        _published.Add(Terms, version).Add(Privacy, version);
        return Create();
    }

    // ------------------------------------------------------------ [UC-104] lịch sử đồng ý của chính mình

    [Fact]
    public async Task History_lists_every_consent_newest_first_and_marks_the_latest_of_each_kind()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        _published.Add(Terms, "2026-11-02", required: true);
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11-02")), null);

        var items = (await service.GetHistoryAsync(User, UserRoles.Student)).Items;

        Assert.Equal([(Terms, "2026-11-02"), (Privacy, "2026-11"), (Terms, "2026-11")], items.Select(i => (i.Kind, i.Version)));
        Assert.Equal([true, true, false], items.Select(i => i.IsLatest));   // điều khoản 2026-11 đã được bản 2026-11-02 thay
        Assert.All(items, i => Assert.True(i.Granted));
    }

    [Fact]
    public async Task History_shows_only_my_own_records()
    {
        var service = WithBoth();
        await service.AcceptAsync(Guid.NewGuid(), UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);

        Assert.Empty((await service.GetHistoryAsync(User, UserRoles.Student)).Items);
    }

    [Fact]
    public async Task History_leaves_out_other_purposes_such_as_the_parent_consent_for_a_child()
    {
        var service = WithBoth();
        _consents.Rows.Add(new Consent
        {
            UserId = User, Purpose = ConsentPurposes.ChildDataProcessing, Granted = true, PolicyVersion = "2026-10", CreatedAt = DateTime.UtcNow,
        });
        await service.AcceptAsync(User, UserRoles.Parent, Req((Terms, "2026-11")), null);

        var items = (await service.GetHistoryAsync(User, UserRoles.Parent)).Items;

        Assert.Equal([Terms], items.Select(i => i.Kind));
    }

    [Fact]
    public async Task History_keeps_a_refusal_row_and_it_is_the_latest()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);
        await _consents.AddAsync(new Consent { UserId = User, Purpose = Terms, Granted = false, PolicyVersion = "2026-11" });

        var items = (await service.GetHistoryAsync(User, UserRoles.Student)).Items;

        Assert.Equal([false, true], items.Select(i => i.Granted));
        Assert.Equal([true, false], items.Select(i => i.IsLatest));
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task History_is_empty_for_internal_roles(string role)
    {
        var service = WithBoth();
        _consents.Rows.Add(new Consent { UserId = User, Purpose = Terms, Granted = true, PolicyVersion = "2026-11", CreatedAt = DateTime.UtcNow });

        Assert.Empty((await service.GetHistoryAsync(User, role)).Items);
    }

    [Fact]
    public async Task History_does_not_expose_the_ip_or_internal_ids()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), "203.0.113.9");

        var json = System.Text.Json.JsonSerializer.Serialize(await service.GetHistoryAsync(User, UserRoles.Student));

        Assert.DoesNotContain("203.0.113.9", json);
        Assert.DoesNotContain(User.ToString(), json);
    }

    // ------------------------------------------------------------ [UC-104] chặn ở server

    private PolicyAcceptanceService WithCache(out MemoryCache cache)
    {
        cache = new MemoryCache(new MemoryCacheOptions());
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        return new PolicyAcceptanceService(_published, _consents, _uow, cache);
    }

    [Fact]
    public async Task New_user_is_blocked_until_accepting_both()
    {
        var service = WithCache(out _);

        Assert.True(await service.MustAcceptAsync(User, UserRoles.Student));
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);
        Assert.True(await service.MustAcceptAsync(User, UserRoles.Student));   // mới đồng ý một trong hai
        await service.AcceptAsync(User, UserRoles.Student, Req((Privacy, "2026-11")), null);
        Assert.False(await service.MustAcceptAsync(User, UserRoles.Student));
    }

    [Fact]
    public async Task Being_blocked_is_never_remembered_so_accepting_unblocks_at_once()
    {
        var service = WithCache(out _);

        Assert.True(await service.MustAcceptAsync(User, UserRoles.Parent));
        Assert.True(await service.MustAcceptAsync(User, UserRoles.Parent));
        await service.AcceptAsync(User, UserRoles.Parent, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);

        Assert.False(await service.MustAcceptAsync(User, UserRoles.Parent));   // không phải đợi hết hạn bộ nhớ
    }

    [Fact]
    public async Task An_ok_result_is_remembered_for_a_short_time_to_spare_the_database()
    {
        var service = WithCache(out _);
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        Assert.False(await service.MustAcceptAsync(User, UserRoles.Student));

        // Admin công bố bản bắt buộc mới: người đã được nhớ là ổn chưa bị chặn ngay, chậm nhất sau OkCacheSeconds giây
        _published.Add(Terms, "2026-11-02", required: true);

        Assert.False(await service.MustAcceptAsync(User, UserRoles.Student));
        Assert.True((await service.GetStatusAsync(User, UserRoles.Student)).MustAccept);   // còn tình trạng thật thì đã thấy ngay
    }

    [Fact]
    public async Task Without_a_cache_every_check_reads_fresh_data()
    {
        _published.Add(Terms, "2026-11").Add(Privacy, "2026-11");
        var service = Create();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        _published.Add(Terms, "2026-11-02", required: true);

        Assert.True(await service.MustAcceptAsync(User, UserRoles.Student));
    }

    [Fact]
    public async Task One_users_ok_result_does_not_unblock_another_user()
    {
        var service = WithCache(out _);
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        Assert.False(await service.MustAcceptAsync(User, UserRoles.Student));

        Assert.True(await service.MustAcceptAsync(Guid.NewGuid(), UserRoles.Student));
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task Internal_roles_are_never_blocked(string role)
    {
        var service = WithCache(out _);
        Assert.False(await service.MustAcceptAsync(User, role));
    }

    [Fact]
    public async Task Nothing_published_blocks_nobody_at_the_gate()
    {
        Assert.False(await Create().MustAcceptAsync(User, UserRoles.Student));
    }

    // ------------------------------------------------------------ xem tình trạng

    [Fact]
    public async Task New_user_must_accept_both_documents()
    {
        var status = await WithBoth().GetStatusAsync(User, UserRoles.Student);

        Assert.True(status.MustAccept);
        Assert.Equal([Terms, Privacy], status.Items.Select(i => i.Kind));
        Assert.All(status.Items, i =>
        {
            Assert.Equal("never_accepted", i.State);
            Assert.True(i.Blocks);
            Assert.Equal("2026-11", i.CurrentVersion);
            Assert.Null(i.AcceptedVersion);
        });
    }

    [Fact]
    public async Task Nothing_published_blocks_nobody()
    {
        var status = await Create().GetStatusAsync(User, UserRoles.Student);

        Assert.False(status.MustAccept);
        Assert.All(status.Items, i => Assert.Equal("not_published", i.State));
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task Internal_roles_are_never_asked(string role)
    {
        var status = await WithBoth().GetStatusAsync(User, role);

        Assert.False(status.MustAccept);
        Assert.Empty(status.Items);
    }

    [Theory]
    [InlineData(UserRoles.Student)]
    [InlineData(UserRoles.Parent)]
    [InlineData(UserRoles.Center)]
    public async Task Student_parent_and_center_are_asked(string role)
    {
        Assert.True((await WithBoth().GetStatusAsync(User, role)).MustAccept);
    }

    [Fact]
    public async Task Newer_required_version_asks_again_and_shows_what_changed()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), "1.2.3.4");
        _published.Add(Terms, "2026-11-02", required: true, summary: "Thêm mục 5");

        var status = await service.GetStatusAsync(User, UserRoles.Student);

        Assert.True(status.MustAccept);
        var terms = status.Items.Single(i => i.Kind == Terms);
        Assert.Equal("update_required", terms.State);
        Assert.Equal("2026-11-02", terms.CurrentVersion);
        Assert.Equal("2026-11", terms.AcceptedVersion);
        Assert.Equal("Thêm mục 5", terms.ChangeSummary);
        Assert.Equal("accepted", status.Items.Single(i => i.Kind == Privacy).State);   // chính sách bảo mật không đổi: không hỏi lại
    }

    [Fact]
    public async Task Newer_optional_version_only_informs()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        _published.Add(Terms, "2026-11-02");

        var status = await service.GetStatusAsync(User, UserRoles.Student);

        Assert.False(status.MustAccept);
        Assert.True(status.HasUpdates);
        Assert.Equal("update_optional", status.Items.Single(i => i.Kind == Terms).State);
    }

    [Fact]
    public async Task A_refusal_row_counts_as_not_accepted()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);
        await _consents.AddAsync(new Consent { UserId = User, Purpose = Terms, Granted = false, PolicyVersion = "2026-11" });

        var terms = (await service.GetStatusAsync(User, UserRoles.Student)).Items.Single(i => i.Kind == Terms);

        Assert.Equal("never_accepted", terms.State);
        Assert.Null(terms.AcceptedVersion);
    }

    [Fact]
    public async Task Consent_with_an_unknown_version_counts_as_not_accepted()
    {
        _consents.Rows.Add(new Consent { UserId = User, Purpose = Terms, Granted = true, PolicyVersion = "v1.0", CreatedAt = DateTime.UtcNow });

        var terms = (await WithBoth().GetStatusAsync(User, UserRoles.Student)).Items.Single(i => i.Kind == Terms);

        Assert.Equal("never_accepted", terms.State);
        Assert.Null(terms.AcceptedVersion);
    }

    [Fact]
    public async Task Another_users_consent_does_not_count()
    {
        var service = WithBoth();
        await service.AcceptAsync(Guid.NewGuid(), UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null);

        Assert.True((await service.GetStatusAsync(User, UserRoles.Student)).MustAccept);
    }

    // ------------------------------------------------------------ ghi đồng ý

    [Fact]
    public async Task Accepting_records_version_purpose_and_ip_and_clears_the_block()
    {
        var service = WithBoth();

        var status = await service.AcceptAsync(User, UserRoles.Parent, Req((Terms, "2026-11"), (Privacy, "2026-11")), "203.0.113.9");

        Assert.False(status.MustAccept);
        Assert.Equal(2, _consents.Rows.Count);
        Assert.All(_consents.Rows, c =>
        {
            Assert.Equal(User, c.UserId);
            Assert.True(c.Granted);
            Assert.Equal("2026-11", c.PolicyVersion);
            Assert.Equal("203.0.113.9", c.GrantedIp);
            Assert.Null(c.GrantedByUserId);   // người dùng tự đồng ý, không ai làm thay
        });
        Assert.Equal([Terms, Privacy], _consents.Rows.Select(c => c.Purpose));
        Assert.Equal(1, _uow.Calls);          // hai dòng ghi trong cùng một giao dịch
    }

    [Fact]
    public async Task Accepting_only_one_document_leaves_the_other_pending()
    {
        var status = await WithBoth().AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);

        Assert.True(status.MustAccept);
        Assert.Equal("accepted", status.Items.Single(i => i.Kind == Terms).State);
        Assert.Equal("never_accepted", status.Items.Single(i => i.Kind == Privacy).State);
    }

    [Fact]
    public async Task Accepting_twice_does_not_add_a_duplicate_row()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);

        Assert.Single(_consents.Rows);
    }

    [Fact]
    public async Task Accepting_a_newer_version_adds_a_new_row_and_keeps_the_old_one()
    {
        var service = WithBoth();
        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11")), null);
        _published.Add(Terms, "2026-11-02", required: true);

        await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11-02")), null);

        Assert.Equal(["2026-11", "2026-11-02"], _consents.Rows.Select(c => c.PolicyVersion));   // lịch sử giữ nguyên
        Assert.Equal("accepted", (await service.GetStatusAsync(User, UserRoles.Student)).Items.Single(i => i.Kind == Terms).State);
    }

    [Fact]
    public async Task Stale_version_is_refused_and_nothing_is_written()
    {
        var service = WithBoth();
        _published.Add(Terms, "2026-11-02", required: true);   // admin vừa công bố trong lúc người dùng đang đọc bản 2026-11

        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() =>
            service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Privacy, "2026-11")), null));

        Assert.Equal("policy_changed", ex.Code);
        Assert.Empty(_consents.Rows);   // tất cả hoặc không gì cả: bản bảo mật còn đúng cũng không được ghi
    }

    [Fact]
    public async Task Reading_the_new_version_then_accepting_works()
    {
        var service = WithBoth();
        _published.Add(Terms, "2026-11-02", required: true);

        var status = await service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11-02"), (Privacy, "2026-11")), null);

        Assert.False(status.MustAccept);
    }

    [Fact]
    public async Task Cannot_accept_a_kind_that_is_not_published()
    {
        _published.Add(Terms, "2026-11");   // chính sách bảo mật chưa công bố

        var ex = await Assert.ThrowsAsync<PolicyRuleException>(() =>
            Create().AcceptAsync(User, UserRoles.Student, Req((Privacy, "2026-11")), null));

        Assert.Equal("policy_changed", ex.Code);
        Assert.Empty(_consents.Rows);
    }

    [Theory]
    [InlineData(PolicyKinds.ChildDataConsent)]   // đồng ý của phụ huynh cho con có luồng riêng (UC-115)
    [InlineData(PolicyKinds.ForumRules)]
    [InlineData("khong_co_loai_nay")]
    public async Task Only_terms_and_privacy_can_be_accepted_here(string kind)
    {
        var service = WithBoth();
        _published.Add(kind, "2026-11");

        await Assert.ThrowsAsync<ArgumentException>(() => service.AcceptAsync(User, UserRoles.Student, Req((kind, "2026-11")), null));
        Assert.Empty(_consents.Rows);
    }

    [Fact]
    public async Task Empty_duplicate_and_blank_requests_are_rejected()
    {
        var service = WithBoth();

        await Assert.ThrowsAsync<ArgumentException>(() => service.AcceptAsync(User, UserRoles.Student, new AcceptPoliciesRequest(null), null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AcceptAsync(User, UserRoles.Student, Req(), null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.AcceptAsync(User, UserRoles.Student, Req((Terms, "2026-11"), (Terms, "2026-11")), null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.AcceptAsync(User, UserRoles.Student, Req((Terms, " ")), null));
        Assert.Empty(_consents.Rows);
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task Internal_roles_cannot_accept(string role)
    {
        await Assert.ThrowsAsync<ArgumentException>(() => WithBoth().AcceptAsync(User, role, Req((Terms, "2026-11")), null));
        Assert.Empty(_consents.Rows);
    }
}
