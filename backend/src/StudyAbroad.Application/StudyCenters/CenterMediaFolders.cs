namespace StudyAbroad.Application.StudyCenters;

/// <summary>
/// Ảnh do trung tâm tự tải lên nằm trong thư mục riêng <c>centers/{id tài khoản chủ}</c>. Chỉ ảnh trong thư mục này mới được
/// xóa trên CDN khi trung tâm thay ảnh hoặc bị xóa hẳn: URL do trung tâm tự nhập có thể trỏ tới ảnh của người khác trong cùng
/// Cloudinary (logo trường, ảnh của trung tâm khác), xóa theo URL sẽ xóa nhầm chúng.
/// </summary>
public static class CenterMediaFolders
{
    private const string Root = "centers";

    /// <summary>Phần đường dẫn thư mục truyền cho <c>MediaUploadRequest.FolderSuffix</c>.</summary>
    public static string For(Guid ownerUserId) => $"{Root}/{ownerUserId:N}";

    /// <summary>
    /// URL có nằm trong thư mục của tài khoản này không. Chỉ xét phần đường dẫn (không xét query hay fragment, vì chúng
    /// có thể bị cài vào để đánh lừa) và đòi có thêm ít nhất một đoạn sau thư mục (tên tệp).
    /// </summary>
    public static bool IsOwnedBy(Guid ownerUserId, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var id = ownerUserId.ToString("N");
        for (var i = 0; i < segments.Length - 2; i++)
        {
            if (segments[i] == Root && string.Equals(segments[i + 1], id, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
