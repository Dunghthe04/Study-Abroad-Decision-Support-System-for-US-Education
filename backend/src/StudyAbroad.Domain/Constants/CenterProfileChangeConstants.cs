namespace StudyAbroad.Domain.Constants;

/// <summary>[UC-92 / UC-114] Các trường tự sửa nhưng cần Moderator kiểm tra.</summary>
public static class CenterProfileChangeFields
{
    public const string Website = "website";
    public const string Fanpage = "fanpage";
}

public static class CenterProfileChangeStatuses
{
    public const string Pending = "pending";
    public const string Passed = "passed";
    public const string Failed = "failed";
}

public static class CenterDeletionRequestStatuses
{
    public const string Pending = "pending";
    public const string Completed = "completed";
    public const string Rejected = "rejected";
}
