using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Quyết định hội thoại nào được cho admin đọc nội dung (UC-90). Hàm thuần, không đọc database, không đọc giờ hệ thống.
/// Luật: người từ 16 tuổi trở lên (hoặc chưa khai ngày sinh) được xem; người DƯỚI 16 tuổi chỉ được xem khi phụ huynh đã đồng ý bản chính sách
/// có nêu mục đích đánh giá chất lượng (ConsentPurposes.QualityReviewMinPolicyVersion trở đi). Tuổi tính TẠI THỜI ĐIỂM của lượt chat, không
/// theo hôm nay, để các hội thoại lúc còn nhỏ không tự mở khi học sinh tròn 16.
/// Tài khoản chưa khai ngày sinh coi như người lớn: đăng ký bắt buộc khai ngày sinh nên đó chủ yếu là tài khoản không qua luồng học sinh
/// (admin, moderator, trung tâm, tài khoản tạo tay).
/// </summary>
public static class AiQualityContentGate
{
    public const int AdultAge = 16;

    public static bool CanReadContent(AiQualityCandidate candidate, string minPolicyVersion = ConsentPurposes.QualityReviewMinPolicyVersion)
    {
        if (candidate.OwnerDateOfBirth is not { } birth)
            return true;
        if (AgeOn(birth, DateOnly.FromDateTime(candidate.CreatedAt)) >= AdultAge)
            return true;
        // Người dưới 16: cần sự đồng ý của phụ huynh với bản chính sách đủ mới
        return candidate.OwnerConsentVersions.Any(v => v is not null && string.CompareOrdinal(v, minPolicyVersion) >= 0);
    }

    /// <summary>Số tuổi tròn của người sinh ngày birth vào ngày on (chưa tới sinh nhật năm đó thì chưa tăng). on trước birth thì 0.</summary>
    public static int AgeOn(DateOnly birth, DateOnly on)
    {
        var age = on.Year - birth.Year;
        if (on < birth.AddYears(age))
            age--;
        return Math.Max(age, 0);
    }
}
