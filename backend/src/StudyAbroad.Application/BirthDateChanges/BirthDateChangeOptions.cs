using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>
/// [UC-107] Tham số cấu hình yêu cầu sửa ngày sinh (appsettings "BirthDateChange").
/// Đặc tả: dung lượng tối đa và danh sách loại giấy tờ được chấp nhận là tham số cấu hình.
/// Mốc tuổi 13, 16, 18, 100 là hằng số dùng chung (AgeGroups, AuthService.MaxAge), không đặt ở đây.
/// </summary>
public class BirthDateChangeOptions
{
    public const string SectionName = "BirthDateChange";

    /// <summary>Dung lượng tối đa của tệp minh chứng (mặc định 5 MB theo UC-107 E3).</summary>
    public long MaxEvidenceBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// Loại giấy tờ được chấp nhận (mã trong BirthDateEvidenceTypes). Để trống thì nhận cả bốn loại.
    /// Không gán sẵn giá trị mặc định vì bộ đọc cấu hình nối thêm phần tử vào danh sách có sẵn chứ không thay thế.
    /// </summary>
    public List<string> EvidenceTypes { get; set; } = [];

    public IReadOnlyList<string> AllowedEvidenceTypes => EvidenceTypes.Count > 0 ? EvidenceTypes : BirthDateEvidenceTypes.All;
}
