using System.Globalization;
using System.Text;

namespace StudyAbroad.Application.Advisor.History;

/// <summary>
/// Con trỏ phân trang (keyset): vị trí của phần tử cuối cùng đã trả về, gồm thời điểm và mã để phân định khi trùng thời điểm.
/// Dùng con trỏ thay vì số trang để danh sách không lặp hay sót khi có phiên hoặc tin mới chen vào giữa hai lần tải.
/// Gửi ra ngoài dưới dạng chuỗi base64url mờ; trình duyệt chỉ đưa lại nguyên văn, không tự dựng.
/// </summary>
public sealed record ChatHistoryCursor(DateTime At, Guid Id)
{
    public string Encode()
    {
        var raw = $"{At.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture)}|{Id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    /// <summary>Đọc con trỏ; null hoặc rỗng là trang đầu. Chuỗi không hợp lệ thì ném ArgumentException.</summary>
    public static ChatHistoryCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return null;
        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(base64)).Split('|');
            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks
                && Guid.TryParseExact(parts[1], "N", out var id))
                return new ChatHistoryCursor(new DateTime(ticks, DateTimeKind.Utc), id);
        }
        catch (FormatException)
        {
        }
        throw new ArgumentException("Con trỏ phân trang không hợp lệ.");
    }
}
