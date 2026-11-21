using System.Text.RegularExpressions;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Che thông tin cá nhân trong nội dung chat trước khi cho admin đọc mẫu hội thoại (UC-90). Hàm thuần, không đọc database, không gọi mạng.
/// Che bằng cách thay chữ khớp mẫu bằng nhãn giữ chỗ ([EMAIL], [SĐT]...), nên đây là che theo quy tắc, KHÔNG phải đảm bảo tuyệt đối:
/// tên người khác học sinh nhắc tới ("mẹ em tên Lan"), địa chỉ nhà hay tên trường có thể còn sót. Vì vậy chỉ là một lớp trong cổng bảo vệ
/// (cùng với chỉ Admin xem, nhật ký người xem và giới hạn số lượt), không được coi là lớp duy nhất.
/// Ưu tiên riêng tư hơn dễ đọc: nghi ngờ thì che (ví dụ dãy từ 9 chữ số liền nhau, kể cả khi đó là số tiền). Không che điểm số, năm học,
/// học phí dạng có dấu chấm, vì admin cần các số đó để đánh giá câu trả lời đúng hay sai.
/// Không phải bộ che trong advisor (advisor/app/services/pii_mask.py): bộ đó viết cho chữ đọc từ bảng điểm, bắt theo nhãn "Họ và tên:".
/// </summary>
public static class ChatTextMasker
{
    public const string EmailMask = "[EMAIL]";
    public const string PhoneMask = "[SĐT]";
    public const string IdMask = "[MÃ SỐ]";
    public const string DateMask = "[NGÀY]";
    public const string NameMask = "[HỌ TÊN]";
    public const string LinkMask = "[LIÊN KẾT MẠNG XÃ HỘI]";

    // Quy tắc nào cũng có giới hạn thời gian để chuỗi lạ không làm treo yêu cầu
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);
    private const RegexOptions Options = RegexOptions.CultureInvariant;

    private static Regex Rule(string pattern, RegexOptions extra = RegexOptions.None) => new(pattern, Options | extra, Timeout);

    private static readonly Regex Email = Rule(@"[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}.\-]+\.\p{L}{2,}");

    // Trang mạng xã hội: thường là trang cá nhân của chính học sinh
    private static readonly Regex SocialLink = Rule(
        @"(?:https?://)?(?:www\.)?(?:facebook|fb|instagram|tiktok|linkedin|zalo)\.(?:com|me|vn)[^\s]*", RegexOptions.IgnoreCase);

    // Số điện thoại Việt Nam: 0xxxxxxxxx hoặc +84xxxxxxxxx (cũng 84xxxxxxxxx), có thể chen dấu cách, chấm, gạch ngang; và số quốc tế +<mã nước>...
    private static readonly Regex Phone = Rule(
        @"(?<![\p{N}])(?:(?:\+?84|0)(?:[\s.\-]?\p{N}){8,10}|\+\p{N}{1,3}(?:[\s.\-]?\p{N}){7,12})(?![\p{N}])");

    // Dãy từ 9 chữ số liền nhau (CCCD, hộ chiếu, mã số...). Số có dấu chấm hay phẩy (59.000, 1,400) và số ngắn (1400, 3.5, 2026) không bị che
    private static readonly Regex LongNumber = Rule(@"(?<![\p{N}.,])\p{N}{9,}(?![\p{N}])");

    // Ngày đầy đủ có năm (dd/mm/yyyy, dd-mm-yyyy, dd.mm.yyyy): thường là ngày sinh. "tháng 9/2026" và "2025-2026" không bị che
    private static readonly Regex FullDate = Rule(@"(?<![\p{N}])\p{N}{1,2}[/.\-]\p{N}{1,2}[/.\-](?:19|20)\p{N}{2}(?![\p{N}])");

    // Tên đi sau nhãn tự giới thiệu: giữ nhãn, che đến 4 từ viết hoa liền sau ("em tên là Nguyễn Văn An")
    private static readonly Regex LabelledName = Rule(
        @"(?<label>(?:họ\s+và\s+tên|họ\s+tên|tên\s+(?:em|tôi|mình|con|cháu)\s+là|(?:em|tôi|mình|con|cháu)\s+tên\s+là|(?:em|tôi|mình|con|cháu)\s+tên|my\s+name\s+is)\s*[:\-]?\s*)" +
        @"(?<name>\p{Lu}\p{Ll}+(?:\s+\p{Lu}\p{Ll}+){0,3})",
        RegexOptions.IgnoreCase);

    /// <summary>
    /// Che thông tin cá nhân trong text. knownNames là họ tên đã biết của người trong cuộc hội thoại (chủ phiên chat): che đúng họ tên đó
    /// và từng phần của nó (viết hoa đúng, nguyên từ), việc mà quy tắc chung không làm được. Null hoặc rỗng thì trả về chuỗi rỗng.
    /// Che xong chạy lại lần nữa cho cùng kết quả.
    /// </summary>
    public static string Mask(string? text, IEnumerable<string?>? knownNames = null)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Thứ tự có ý nghĩa: số điện thoại trước dãy số dài, để số điện thoại ra nhãn [SĐT] chứ không thành [MÃ SỐ]
        var masked = Email.Replace(text, EmailMask);
        masked = SocialLink.Replace(masked, LinkMask);
        masked = Phone.Replace(masked, PhoneMask);
        masked = LongNumber.Replace(masked, IdMask);
        masked = FullDate.Replace(masked, DateMask);
        masked = LabelledName.Replace(masked, m => m.Groups["label"].Value + NameMask);
        return MaskKnownNames(masked, knownNames);
    }

    private static string MaskKnownNames(string text, IEnumerable<string?>? knownNames)
    {
        var names = (knownNames ?? [])
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (names.Count == 0)
            return text;

        // Họ tên đầy đủ (không phân biệt hoa thường, dấu cách linh hoạt), rồi từng phần của tên (phân biệt hoa thường, nguyên từ)
        // để "an toàn" viết thường không bị che nhầm khi tên là "An"
        foreach (var name in names.OrderByDescending(n => n.Length))
        {
            var words = name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 1)
            {
                var full = string.Join(@"\s+", words.Select(Regex.Escape));
                text = Regex.Replace(text, $@"(?<!\p{{L}}){full}(?!\p{{L}})", NameMask, Options | RegexOptions.IgnoreCase, Timeout);
            }
            foreach (var word in words.Where(w => w.Length >= 2).Distinct(StringComparer.Ordinal))
                text = Regex.Replace(text, $@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})", NameMask, Options, Timeout);
        }
        return text;
    }
}
