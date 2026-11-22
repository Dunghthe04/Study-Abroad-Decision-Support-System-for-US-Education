using System.Text;

namespace StudyAbroad.Application.Advisor.History;

/// <summary>
/// Tiêu đề của một phiên chat trong lịch sử: lấy từ câu hỏi đầu tiên của học sinh, gom khoảng trắng, bỏ ký tự điều khiển,
/// dài quá MaxLength thì cắt và thêm dấu "…". Không dùng AI (thêm độ trễ và chi phí cho một việc nhỏ).
/// Cùng quy tắc với câu SQL điền tiêu đề cho phiên cũ trong migration ChatSessionTitleAndActivityIndex.
/// </summary>
public static class ChatSessionTitle
{
    public const int MaxLength = 60;

    /// <summary>Tên người dùng tự đặt khi đổi tên phiên: dài hơn tiêu đề tự động nhưng vẫn gọn (cột chứa được 200).</summary>
    public const int MaxUserLength = 100;

    /// <summary>Tên hiện khi phiên không có tiêu đề (ví dụ lượt đầu là bấm nút không kèm chữ).</summary>
    public const string Untitled = "Cuộc trò chuyện";

    /// <summary>Tiêu đề từ câu hỏi; null nếu câu rỗng hoặc chỉ có khoảng trắng.</summary>
    public static string? From(string? text)
    {
        var clean = Clean(text);
        if (clean is null)
            return null;
        return clean.Length <= MaxLength ? clean : clean[..MaxLength].TrimEnd() + "…";
    }

    /// <summary>
    /// Tên người dùng tự đặt: gom khoảng trắng như trên nhưng KHÔNG tự cắt. Rỗng hoặc dài quá MaxUserLength thì ném ArgumentException
    /// (người dùng cần biết tên mình gõ không được nhận, thay vì bị cắt lặng lẽ).
    /// </summary>
    public static string FromUser(string? text)
    {
        var clean = Clean(text) ?? throw new ArgumentException("Tên cuộc trò chuyện không được để trống.");
        if (clean.Length > MaxUserLength)
            throw new ArgumentException($"Tên cuộc trò chuyện tối đa {MaxUserLength} ký tự.");
        return clean;
    }

    // Ký tự điều khiển (xuống dòng, tab...) và mọi khoảng trắng liền nhau thành một dấu cách, bỏ khoảng trắng hai đầu; rỗng thì null
    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;
        foreach (var ch in text)
        {
            var space = char.IsWhiteSpace(ch) || char.IsControl(ch);
            if (space)
            {
                if (!lastWasSpace && builder.Length > 0)
                    builder.Append(' ');
                lastWasSpace = true;
                continue;
            }
            builder.Append(ch);
            lastWasSpace = false;
        }
        var clean = builder.ToString().TrimEnd();
        return clean.Length == 0 ? null : clean;
    }
}
