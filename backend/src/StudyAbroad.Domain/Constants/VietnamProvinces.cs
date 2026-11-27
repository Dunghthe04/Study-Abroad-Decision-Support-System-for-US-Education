using System.Globalization;
using System.Text;

namespace StudyAbroad.Domain.Constants;

/// <summary>
/// 34 tỉnh/thành Việt Nam sau sáp nhập 2025. Học sinh chọn một tên trong danh sách (StudentProfile.Province);
/// trung tâm nhập City bằng chữ tự do nên khi so phải chuẩn hóa qua <see cref="Normalize"/>.
/// </summary>
public static class VietnamProvinces
{
    public static readonly IReadOnlyList<string> All =
    [
        "Hà Nội", "Huế", "Lai Châu", "Điện Biên", "Sơn La", "Lạng Sơn", "Quảng Ninh", "Thanh Hóa",
        "Nghệ An", "Hà Tĩnh", "Cao Bằng", "Tuyên Quang", "Lào Cai", "Thái Nguyên", "Phú Thọ", "Bắc Ninh",
        "Hưng Yên", "Hải Phòng", "Ninh Bình", "Quảng Trị", "Đà Nẵng", "Quảng Ngãi", "Gia Lai", "Khánh Hòa",
        "Lâm Đồng", "Đắk Lắk", "TP. Hồ Chí Minh", "Đồng Nai", "Tây Ninh", "Cần Thơ", "Vĩnh Long",
        "Đồng Tháp", "Cà Mau", "An Giang",
    ];

    // tên gọi khác hay gặp trong City của trung tâm → tên chuẩn đã chuẩn hóa
    private static readonly Dictionary<string, string> Aliases = new()
    {
        ["hcm"] = "ho chi minh",
        ["hcmc"] = "ho chi minh",
        ["tphcm"] = "ho chi minh",
        ["sg"] = "ho chi minh",
        ["sai gon"] = "ho chi minh",
        ["saigon"] = "ho chi minh",
        ["hn"] = "ha noi",
        ["hanoi"] = "ha noi",
        ["hp"] = "hai phong",
        ["thua thien hue"] = "hue",
        ["danang"] = "da nang",
    };

    private static readonly string[] Prefixes = ["thanh pho ", "tp. ", "tp.", "tp ", "tinh "];

    /// <summary>bỏ dấu, viết thường, bỏ tiền tố "TP.", "Thành phố", "Tỉnh", đổi tên gọi khác về tên chuẩn</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";
        var s = RemoveDiacritics(value.Trim().ToLowerInvariant());
        foreach (var p in Prefixes)
        {
            if (s.StartsWith(p, StringComparison.Ordinal))
            {
                s = s[p.Length..].Trim();
                break;
            }
        }
        s = string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return Aliases.TryGetValue(s, out var canonical) ? canonical : s;
    }

    /// <summary>tên chuẩn trong <see cref="All"/> khớp với giá trị nhập; null nếu không khớp tỉnh nào</summary>
    public static string? Resolve(string? value)
    {
        var key = Normalize(value);
        if (key.Length == 0)
            return null;
        return All.FirstOrDefault(p => Normalize(p) == key);
    }

    /// <summary>true khi hai tên chỉ cùng một tỉnh (vd "TP.HCM" và "TP. Hồ Chí Minh")</summary>
    public static bool SameProvince(string? a, string? b)
    {
        var na = Normalize(a);
        return na.Length > 0 && na == Normalize(b);
    }

    private static string RemoveDiacritics(string s)
    {
        var decomposed = s.Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
