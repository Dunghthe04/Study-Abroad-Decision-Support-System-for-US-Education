using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [USAS-2053 / UC-74 / UC-44] Kết quả kiểm tra bộ lọc nội dung tin nhắn yêu cầu liên hệ.
/// </summary>
public sealed record LeadMessageFilterResult(bool IsFlagged, string? FlagReason);

/// <summary>
/// [USAS-2053 / UC-74 / UC-44] Bộ lọc nội dung dùng chung cho tin nhắn trao đổi giữa Trung tâm và Học sinh/Phụ huynh.
/// Tự động phát hiện và gắn cờ (không chặn lưu, để Moderator xem xét ở UC-58) khi nội dung chứa:
/// 1) Số điện thoại liên hệ trực tiếp (VN / quốc tế / tổng đài).
/// 2) Nội dung quảng cáo, chào mời giao dịch ngoài nền tảng hoặc liên kết ngoài.
/// </summary>
public static partial class LeadMessageContentFilter
{
    public const int MaxFlagReasonLength = 500;

    // Số điện thoại Việt Nam (02x/03x/05x/07x/08x/09x, 10–11 số, hỗ trợ dấu cách/chấm/gạch ngang/+84/84/ngoặc đơn)
    // Tránh bắt nhầm học phí (500.000.000), điểm GPA/SAT/IELTS hoặc ngày tháng (09/11/2026).
    [GeneratedRegex(
        @"(?<!\d)(?:\(?(?:\+|00)?84\)?[\s.\-]*|\(?0)(?:2\d|[35789]\d)\)?(?:[\s.\-]*\d){7,8}(?!\d)",
        RegexOptions.Compiled)]
    private static partial Regex VietnamPhoneRegex();

    // Tổng đài 1800 / 1900 (ví dụ: 1900 1234, 1800-567890)
    [GeneratedRegex(
        @"(?<!\d)(?:1800|1900)[\s.\-]?\d{4,6}(?!\d)",
        RegexOptions.Compiled)]
    private static partial Regex HotlinePhoneRegex();

    // Số điện thoại quốc tế có mã quốc gia +<cc> với định dạng nhóm số (ví dụ: +1 (617) 555-0199)
    [GeneratedRegex(
        @"(?<!\d)\+(?!84\b)\d{1,3}[\s.\-]?\(?\d{3}\)?[\s.\-]?\d{3}[\s.\-]?\d{4}(?!\d)",
        RegexOptions.Compiled)]
    private static partial Regex InternationalPhoneRegex();

    // Từ khóa số điện thoại đi kèm chuỗi số >= 9 chữ số (ví dụ: "sdt: 090 123 456", "zalo 0912*345*678")
    [GeneratedRegex(
        @"(?i)\b(?:sdt|sđt|so dien thoai|dien\s*thoai|tel|hotline|zalo|whatsapp|viber|telegram|phone|call)\b\s*[:\-]?\s*(?:\+?\d[\d\s.\-*]{7,15}\d)",
        RegexOptions.Compiled)]
    private static partial Regex KeywordPhoneRegex();

    // Liên kết ngoài / tên miền mạng xã hội / biểu mẫu / rút gọn link
    [GeneratedRegex(
        @"(?i)(?:https?://\S+|www\.\S+|\b(?:bit\.ly|tinyurl\.com|goo\.gl|linktr\.ee|forms\.gle|zalo\.me|zaloapp\.com|t\.me|wa\.me|m\.me|fb\.com|facebook\.com|instagram\.com|tiktok\.com|youtube\.com|youtu\.be|shopee\.vn|lazada\.vn)/\S*|\bdocs\.google\.com/forms\S*)",
        RegexOptions.Compiled)]
    private static partial Regex ExternalUrlRegex();

    private static readonly string[] NormalizedAdPhrases =
    [
        "khuyen mai",
        "khuyen mai dac biet",
        "khuyen mai hoc phi",
        "giam gia",
        "giam gia hoc phi",
        "uu dai dac biet",
        "uu dai doc quyen",
        "ma giam gia",
        "voucher",
        "tang ngay",
        "mien phi 100%",
        "cam ket dau",
        "bao dau",
        "chi hom nay",
        "nhanh tay dang ky",
        "dang ky ngay hom nay",
        "ket ban zalo",
        "lien he zalo",
        "lien he qua zalo",
        "nhan tin qua zalo",
        "nhan qua zalo",
        "add zalo",
        "ib zalo",
        "inbox zalo",
        "inbox ngay",
        "ib ngay",
        "nhan tin rieng",
        "lien he ngoai he thong",
        "giao dich ngoai nen tang",
        "chuyen khoan",
        "dat coc",
        "truy cap link",
        "click vao link",
        "ban sim",
        "cho vay",
        "vay von",
        "ca do",
        "casino",
        "xo so",
        "kiem tien online",
        "special offer",
        "limited time offer",
        "promo code",
        "discount code",
        "guaranteed admission",
        "click here",
    ];

    /// <summary>
    /// Kiểm tra nội dung tin nhắn và trả về trạng thái gắn cờ cùng lý do (nếu vi phạm).
    /// </summary>
    public static LeadMessageFilterResult Inspect(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new LeadMessageFilterResult(false, null);

        // Loại bỏ ký tự ẩn zero-width để tránh né bộ lọc (ví dụ: 0912\u200B345\u200B678)
        var sanitized = StripInvisibleCharacters(content).Trim();
        if (string.IsNullOrEmpty(sanitized))
            return new LeadMessageFilterResult(false, null);

        var reasons = new List<string>(2);

        if (ContainsPhoneNumber(sanitized))
            reasons.Add("Chứa số điện thoại liên hệ");

        if (ContainsAdvertisement(sanitized))
            reasons.Add("Chứa nội dung quảng cáo hoặc liên kết ngoài");

        if (reasons.Count == 0)
            return new LeadMessageFilterResult(false, null);

        var combined = string.Join("; ", reasons);
        if (combined.Length > MaxFlagReasonLength)
            combined = combined[..MaxFlagReasonLength];

        return new LeadMessageFilterResult(true, combined);
    }

    private static bool ContainsPhoneNumber(string text)
    {
        if (VietnamPhoneRegex().IsMatch(text) ||
            HotlinePhoneRegex().IsMatch(text) ||
            InternationalPhoneRegex().IsMatch(text))
        {
            return true;
        }

        var normalized = NormalizeForKeywordMatch(text);
        foreach (Match match in KeywordPhoneRegex().Matches(normalized))
        {
            // Yêu cầu ít nhất 9 chữ số để tránh bắt nhầm ngày tháng (DD-MM-YYYY chỉ có 8 chữ số)
            if (CountDigits(match.Value) >= 9)
                return true;
        }

        return false;
    }

    private static int CountDigits(string value)
    {
        var count = 0;
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
                count++;
        }
        return count;
    }

    private static string StripInvisibleCharacters(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.Format)
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static bool ContainsAdvertisement(string text)
    {
        if (ExternalUrlRegex().IsMatch(text))
            return true;

        var normalized = NormalizeForKeywordMatch(text);
        foreach (var phrase in NormalizedAdPhrases)
        {
            if (ContainsWholePhrase(normalized, phrase))
                return true;
        }

        return false;
    }

    private static bool ContainsWholePhrase(string normalizedText, string phrase)
    {
        var idx = normalizedText.IndexOf(phrase, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var leftOk = idx == 0 || !char.IsLetterOrDigit(normalizedText[idx - 1]);
            var end = idx + phrase.Length;
            var rightOk = end >= normalizedText.Length || !char.IsLetterOrDigit(normalizedText[end]);
            if (leftOk && rightOk)
                return true;

            idx = normalizedText.IndexOf(phrase, idx + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string NormalizeForKeywordMatch(string input)
    {
        var decomposed = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            var mapped = ch switch
            {
                'đ' or 'Đ' => 'd',
                _ => char.ToLowerInvariant(ch),
            };
            sb.Append(mapped);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}

