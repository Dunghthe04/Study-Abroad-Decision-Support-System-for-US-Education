namespace StudyAbroad.Domain.Constants;

/// <summary>Chuyển hồ sơ học thuật phụ huynh đã nhập (trước khi liên kết) sang tài khoản con.</summary>
public static class ProfileTransferStatuses
{
    /// <summary>Con đã có hồ sơ: chờ con chọn dùng bản của phụ huynh hay giữ bản của mình.</summary>
    public const string Offered = "offered";
    /// <summary>Đã chép hồ sơ phụ huynh sang con (con chưa có hồ sơ, hoặc con chọn dùng bản của phụ huynh).</summary>
    public const string Copied = "copied";
    /// <summary>Con chọn giữ bản của mình.</summary>
    public const string Declined = "declined";
    /// <summary>Phụ huynh có nhiều con chọn không chuyển hồ sơ cho con nào.</summary>
    public const string Skipped = "skipped";
}
