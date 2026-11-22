namespace StudyAbroad.Domain.Constants;

/// <summary>Giá trị <c>forum_posts.category</c>. Chat realtime dùng một mã riêng để không lẫn với chủ đề diễn đàn sau này.</summary>
public static class ForumCategories
{
    public const string CommunityChat = "community_chat";
}

/// <summary>Giá trị <c>forum_posts.status</c>.</summary>
public static class ForumPostStatuses
{
    public const string Visible = "visible";
    public const string Hidden = "hidden";
    public const string Removed = "removed";
}

/// <summary>Giá trị <c>forum_reports.status</c>.</summary>
public static class ForumReportStatuses
{
    public const string Open = "open";
    public const string Actioned = "actioned";
    public const string Dismissed = "dismissed";
}
