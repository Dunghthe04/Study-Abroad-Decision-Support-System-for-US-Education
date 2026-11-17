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
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string NeedsFix = "needs_fix";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Pending, Approved, Rejected, NeedsFix };
}

public static class SlmSampleSources
{
    public const string Manual = "manual";
    public const string Excel = "excel";
    public const string ChatUnanswered = "chat-unanswered";

    public static readonly IReadOnlyList<string> All = new[] { Manual, Excel, ChatUnanswered };
}

public static class SlmSampleHistoryActions
{
    public const string Create = "create";
    public const string Edit = "edit";
    public const string Submit = "submit";
    public const string Review = "review";
    public const string Delete = "delete";
    public const string Capture = "capture";
}
