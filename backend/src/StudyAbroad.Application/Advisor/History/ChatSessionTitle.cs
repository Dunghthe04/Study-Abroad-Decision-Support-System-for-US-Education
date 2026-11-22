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

    /// <summary>Tên hiện khi phiên không có tiêu đề (ví dụ lượt đầu là bấm nút không kèm chữ).</summary>
    public const string Untitled = "Cuộc trò chuyện";

    /// <summary>Tiêu đề từ câu hỏi; null nếu câu rỗng hoặc chỉ có khoảng trắng.</summary>
    public static string? From(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Ký tự điều khiển (xuống dòng, tab...) và mọi khoảng trắng liền nhau thành một dấu cách
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
        if (clean.Length == 0)
            return null;
        return clean.Length <= MaxLength ? clean : clean[..MaxLength].TrimEnd() + "…";
    }
}
