namespace StudyAbroad.Domain.Constants;

/// <summary>
/// Quy tắc chặn một chức năng theo tuổi.
/// <paramref name="MinAge"/>: tuổi tối thiểu được dùng.
/// <paramref name="ParentConsentUnlocks"/>: dưới tuổi đó nhưng phụ huynh đã đồng ý thì vẫn được dùng.
/// </summary>
public sealed record AgeRule(int MinAge, bool ParentConsentUnlocks);

/// <summary>Kết quả bị chặn: chức năng nào, và phụ huynh đồng ý có mở được không.</summary>
public sealed record AgeBlock(string Feature, bool NeedParentConsent);

/// <summary>
/// [Q11] Bảng duy nhất chứa mọi quy tắc chặn chức năng theo tuổi (Luật BVDLCN 91/2025 Điều 24).
/// Thêm chặn mới: thêm 1 dòng vào <see cref="Rules"/>, gắn [Authorize(Policy = AgeFeatures.X)] lên endpoint,
/// bọc nút/trang bằng &lt;AgeGate feature="x"&gt;. Không phải sửa handler hay DB.
/// </summary>
public static class AgeFeatures
{
    /// <summary>Lưu hồ sơ (wizard), upload bảng điểm, gợi ý trường, chat AI (gửi dữ liệu cá nhân cho AI).</summary>
    public const string ProcessStudentData = "process_student_data";

    /// <summary>Chia sẻ hồ sơ cho trung tâm (bảng leads). Chưa có API; API tạo lead phải gắn policy này.</summary>
    public const string ShareWithCenter = "share_with_center";

    public static readonly IReadOnlyDictionary<string, AgeRule> Rules = new Dictionary<string, AgeRule>
    {
        [ProcessStudentData] = new(MinAge: AgeGroups.ChildMaxAgeExclusive, ParentConsentUnlocks: true), // dưới 16 cần phụ huynh đồng ý
        [ShareWithCenter] = new(MinAge: AgeGroups.AdultAge, ParentConsentUnlocks: false),                 // dưới 18 chặn hẳn
    };

    /// <summary>
    /// Chỉ áp cho học sinh có ngày sinh. Phụ huynh, center, admin và tài khoản cũ chưa có ngày sinh luôn được qua.
    /// Trả null nếu được dùng.
    /// </summary>
    public static AgeBlock? Check(string feature, string role, DateOnly? dateOfBirth, DateOnly today, bool hasParentConsent)
    {
        if (!Rules.TryGetValue(feature, out var rule))
            throw new ArgumentException($"Chưa khai báo quy tắc tuổi cho chức năng '{feature}'.", nameof(feature));
        if (role != UserRoles.Student || dateOfBirth is null)
            return null;
        if (AgePolicy.GetAge(dateOfBirth.Value, today) >= rule.MinAge)
            return null;
        if (rule.ParentConsentUnlocks && hasParentConsent)
            return null;
        return new AgeBlock(feature, rule.ParentConsentUnlocks);
    }

    public static List<string> GetBlocked(string role, DateOnly? dateOfBirth, DateOnly today, bool hasParentConsent) =>
        Rules.Keys.Where(f => Check(f, role, dateOfBirth, today, hasParentConsent) != null).ToList();

    /// <summary>Có quy tắc nào mà phụ huynh đồng ý sẽ đổi kết quả không; không thì khỏi tra DB.</summary>
    public static bool ConsentMatters(string role, DateOnly? dateOfBirth, DateOnly today) =>
        Rules.Any(r => r.Value.ParentConsentUnlocks && Check(r.Key, role, dateOfBirth, today, hasParentConsent: false) != null);
}
