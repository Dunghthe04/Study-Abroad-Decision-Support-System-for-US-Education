using Microsoft.AspNetCore.Http;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Api.Extensions;

/// <summary>
/// Câu tiếng Việt cho lỗi server trả về mà không kèm lý do: Forbid(), NotFound(), Unauthorized() trần,
/// sai vai trò ở [Authorize(Roles = ...)], token hết hạn, lỗi 500 chưa bắt...
/// Không có câu này thì frontend chỉ hiện "Forbidden", "Not Found" và người dùng không biết vì sao.
/// </summary>
public static class DefaultErrorMessages
{
    public static string? For(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Thông tin gửi lên chưa hợp lệ. Bạn kiểm tra lại rồi thử lại nhé.",
        StatusCodes.Status401Unauthorized => "Phiên đăng nhập đã hết hạn hoặc bạn chưa đăng nhập. Bạn đăng nhập lại rồi thử lại nhé.",
        StatusCodes.Status403Forbidden => "Tài khoản của bạn không được dùng chức năng này.",
        StatusCodes.Status404NotFound => "Không tìm thấy dữ liệu bạn yêu cầu. Có thể dữ liệu đã bị xóa.",
        StatusCodes.Status405MethodNotAllowed => "Thao tác này không được hỗ trợ.",
        StatusCodes.Status409Conflict => "Dữ liệu vừa bị thay đổi ở nơi khác. Bạn tải lại trang rồi thử lại nhé.",
        StatusCodes.Status413PayloadTooLarge => "Tệp gửi lên quá lớn.",
        StatusCodes.Status415UnsupportedMediaType => "Định dạng dữ liệu gửi lên không được hỗ trợ.",
        StatusCodes.Status429TooManyRequests => "Bạn thao tác hơi nhanh. Đợi một chút rồi thử lại nhé.",
        >= 500 => "Máy chủ đang gặp sự cố. Bạn thử lại sau ít phút nhé.",
        _ => null,
    };

    /// <summary>Gắn vào AddProblemDetails: chỉ điền khi lỗi chưa có Detail, không đè lý do controller đã viết.</summary>
    public static void FillMissingDetail(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        if (!string.IsNullOrWhiteSpace(problem.Detail)) return;
        problem.Detail = For(problem.Status ?? context.HttpContext.Response.StatusCode);
    }

    /// <summary>Sai vai trò: nói rõ chức năng dành cho loại tài khoản nào.</summary>
    public static string WrongRole(IEnumerable<string> allowedRoles)
    {
        var names = allowedRoles.Select(RoleName).Distinct().ToList();
        return names.Count == 0
            ? For(StatusCodes.Status403Forbidden)!
            : $"Chức năng này chỉ dành cho tài khoản {string.Join(" hoặc ", names)}. Bạn đang đăng nhập bằng loại tài khoản khác.";
    }

    private static string RoleName(string role) => role switch
    {
        UserRoles.Student => "học sinh",
        UserRoles.Parent => "phụ huynh",
        UserRoles.Center => "trung tâm tư vấn",
        UserRoles.Moderator => "kiểm duyệt viên",
        UserRoles.Admin => "quản trị viên",
        _ => role,
    };
}
