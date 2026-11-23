using StudyAbroad.Application.Policies;
using StudyAbroad.Domain.Constants;
using static StudyAbroad.Application.Policies.PolicyAcceptanceState;

namespace StudyAbroad.UnitTests;

// [UC-104] Khi nào người dùng phải đồng ý (lại) một loại tài liệu.
public class PolicyAcceptanceRulesTests
{
    private static PublishedPolicyVersion V(string version, bool required = false) => new(version, required);

    [Fact]
    public void No_published_version_blocks_nobody()
    {
        Assert.Equal(NotPublished, PolicyAcceptanceRules.Evaluate([], null));
        Assert.Equal(NotPublished, PolicyAcceptanceRules.Evaluate([], "2026-11"));
        Assert.False(PolicyAcceptanceRules.Blocks(NotPublished));
    }

    [Fact]
    public void Never_accepted_must_accept()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11")], null);
        Assert.Equal(NeverAccepted, state);
        Assert.True(PolicyAcceptanceRules.Blocks(state));
    }

    [Fact]
    public void Accepted_current_version_is_fine()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11")], "2026-11");
        Assert.Equal(Accepted, state);
        Assert.False(PolicyAcceptanceRules.Blocks(state));
    }

    [Fact]
    public void Newer_required_version_blocks()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11"), V("2026-11-02", required: true)], "2026-11");
        Assert.Equal(UpdateRequired, state);
        Assert.True(PolicyAcceptanceRules.Blocks(state));
    }

    [Fact]
    public void Newer_optional_version_does_not_block()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11"), V("2026-11-02")], "2026-11");
        Assert.Equal(UpdateOptional, state);
        Assert.False(PolicyAcceptanceRules.Blocks(state));
    }

    [Fact]
    public void Required_version_in_the_middle_still_blocks_when_latest_is_optional()
    {
        // 02 bắt buộc, 03 không bắt buộc: người đồng ý 2026-11 chưa từng đồng ý 02 nên vẫn phải đồng ý
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11"), V("2026-11-02", required: true), V("2026-11-03")], "2026-11");
        Assert.Equal(UpdateRequired, state);
    }

    [Fact]
    public void Accepting_the_required_version_clears_the_block_even_if_a_later_optional_one_exists()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11"), V("2026-11-02", required: true), V("2026-11-03")], "2026-11-02");
        Assert.Equal(UpdateOptional, state);
        Assert.False(PolicyAcceptanceRules.Blocks(state));
    }

    [Fact]
    public void The_required_flag_of_the_accepted_version_itself_does_not_count()
    {
        // Đã đồng ý 2026-11-02 (chính bản bắt buộc) thì chỉ xét các bản SAU nó
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11", required: true), V("2026-11-02", required: true)], "2026-11-02");
        Assert.Equal(Accepted, state);
    }

    [Fact]
    public void Versions_compare_in_time_order_across_months_and_suffixes()
    {
        // 2026-11-10 phải sau 2026-11-02, và 2026-12 sau cả hai
        var all = new[] { V("2026-11"), V("2026-11-02"), V("2026-11-10", required: true), V("2026-12") };
        Assert.Equal(UpdateRequired, PolicyAcceptanceRules.Evaluate(all, "2026-11-02"));
        Assert.Equal(UpdateOptional, PolicyAcceptanceRules.Evaluate(all, "2026-11-10"));
        Assert.Equal(Accepted, PolicyAcceptanceRules.Evaluate(all, "2026-12"));
    }

    [Fact]
    public void Order_of_the_input_list_does_not_matter()
    {
        var state = PolicyAcceptanceRules.Evaluate([V("2026-11-03"), V("2026-11-02", required: true), V("2026-11")], "2026-11");
        Assert.Equal(UpdateRequired, state);
    }

    [Theory]
    [InlineData("v1.0")]          // bản ghi cũ viết cứng (khách liên hệ): chữ v đứng sau chữ số, không được coi là "mới hơn mọi bản"
    [InlineData("2099-01")]       // phiên bản chưa từng công bố
    [InlineData("")]
    public void An_accepted_version_that_was_never_published_counts_as_not_accepted(string recorded)
    {
        Assert.Equal(NeverAccepted, PolicyAcceptanceRules.Evaluate([V("2026-11")], recorded));
    }

    [Fact]
    public void Account_kinds_are_terms_and_privacy_only()
    {
        Assert.Equal([PolicyKinds.TermsOfUse, PolicyKinds.PrivacyPolicy], PolicyAcceptanceRules.AccountKinds);
        Assert.Equal(PolicyKinds.TermsOfUse, ConsentPurposes.TermsOfUse);
        Assert.Equal(PolicyKinds.PrivacyPolicy, ConsentPurposes.PrivacyPolicy);
        // Cột purpose dài tối đa 32 ký tự
        Assert.All(PolicyAcceptanceRules.AccountKinds, k => Assert.True(k.Length <= 32));
    }
}
