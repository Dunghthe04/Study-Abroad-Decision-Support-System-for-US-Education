namespace StudyAbroad.Domain.Constants;

public static class SlmSampleTypes
{
    public const string CollectProfile = "collect_profile";
    public const string ProfileReview = "profile_review";
    public const string Scenario = "scenario";
    public const string Knowledge = "knowledge";
    public const string CenterReferral = "center_referral";
    public const string OutOfScope = "out_of_scope";

    public static readonly IReadOnlyList<string> All = new[]
    {
        CollectProfile, ProfileReview, Scenario, Knowledge, CenterReferral, OutOfScope
    };
}

public static class SlmSampleSplits
{
    public const string Train = "train";
    public const string Val = "val";
    public const string Test = "test";

    public static readonly IReadOnlyList<string> All = new[] { Train, Val, Test };
}

public static class SlmReviewStatuses
{
    public const string Draft = "draft";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string NeedsFix = "needs_fix";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Approved, Rejected, NeedsFix };
}
