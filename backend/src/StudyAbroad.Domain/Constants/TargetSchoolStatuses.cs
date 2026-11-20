namespace StudyAbroad.Domain.Constants;

/// <summary>Cột Status của bảng target_schools (tối đa 16 ký tự).</summary>
public static class TargetSchoolStatuses
{
    /// <summary>Trường con tự thêm, hoặc con đã nhận gợi ý của phụ huynh.</summary>
    public const string Considering = "considering";

    /// <summary>Phụ huynh gợi ý, con chưa xem. Chưa là trường mục tiêu của con: không đếm, không hiện trong danh sách chính.
    /// Con bấm Thêm thì đổi thành considering, bấm Bỏ qua thì xóa dòng. Lời nhắn của phụ huynh nằm ở Notes.</summary>
    public const string ParentSuggested = "parent_suggested";

    public static bool IsParentSuggestion(string? status) =>
        string.Equals(status, ParentSuggested, StringComparison.OrdinalIgnoreCase);
}
