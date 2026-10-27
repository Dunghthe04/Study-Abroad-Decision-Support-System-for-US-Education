namespace StudyAbroad.Domain.Constants;

/// <summary>Nhóm tuổi theo luật VN: trẻ em dưới 16 (Luật Trẻ em 2016), chưa thành niên 16–17, thành niên từ 18 (Bộ luật Dân sự).</summary>
public static class AgeGroups
{
    /// <summary>Dưới 16: xử lý dữ liệu cá nhân cần cha mẹ đồng ý (Luật BVDLCN 91/2025 Điều 24).</summary>
    public const string Child = "child";
    /// <summary>16–17: tự đồng ý được nhưng vẫn là người chưa thành niên.</summary>
    public const string Minor = "minor";
    public const string Adult = "adult";

    public const int ChildMaxAgeExclusive = 16;
    public const int AdultAge = 18;
}

public static class AgePolicy
{
    /// <summary>Việt Nam không có giờ mùa hè nên cố định UTC+7, không phụ thuộc dữ liệu múi giờ của máy chủ.</summary>
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateOnly TodayInVietnam(DateTime utcNow) => DateOnly.FromDateTime(utcNow + VietnamOffset);

    /// <summary>Tuổi tròn tại ngày <paramref name="today"/>. Sinh 29/2 thì năm không nhuận coi như đủ tuổi từ 1/3.</summary>
    public static int GetAge(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (today.Month < dateOfBirth.Month || (today.Month == dateOfBirth.Month && today.Day < dateOfBirth.Day))
            age--;
        return age;
    }

    /// <summary>Null khi chưa có ngày sinh (tài khoản cũ, center, admin): không áp giới hạn theo tuổi.</summary>
    public static string? GetAgeGroup(DateOnly? dateOfBirth, DateOnly today)
    {
        if (dateOfBirth is null) return null;
        var age = GetAge(dateOfBirth.Value, today);
        return age < AgeGroups.ChildMaxAgeExclusive ? AgeGroups.Child
            : age < AgeGroups.AdultAge ? AgeGroups.Minor
            : AgeGroups.Adult;
    }
}
