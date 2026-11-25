namespace StudyAbroad.Domain.Constants;

/// <summary>[UC-151] Lý do báo cáo đánh giá trung tâm.</summary>
public static class CenterReviewReportReasons
{
    public const string OffensiveLanguage = "offensive_language";
    public const string PersonalInfo = "personal_info";
    public const string NotRealExperience = "not_real_experience";
    public const string SpamAd = "spam_ad";
    public const string Other = "other";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        OffensiveLanguage,
        PersonalInfo,
        NotRealExperience,
        SpamAd,
        Other,
    };
}

/// <summary>[UC-151 / UC-53] Trạng thái báo cáo đánh giá trung tâm.</summary>
public static class CenterReviewReportStatuses
{
    public const string Open = "open";
    public const string ResolvedRemoved = "resolved_removed";
    public const string Dismissed = "dismissed";
}
