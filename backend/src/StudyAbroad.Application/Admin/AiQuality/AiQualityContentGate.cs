using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Quyết định hội thoại nào được cho admin đọc nội dung (UC-90). Hàm thuần, không đọc database, không đọc giờ hệ thống.
/// Luật: người từ 16 tuổi trở lên (hoặc chưa khai ngày sinh) được xem; người DƯỚI 16 tuổi chỉ được xem khi phụ huynh đã đồng ý bản chính sách
/// có nêu mục đích đánh giá chất lượng (ConsentPurposes.QualityReviewMinPolicyVersion trở đi). Tuổi tính TẠI THỜI ĐIỂM của lượt chat (theo
/// ngày Việt Nam, như AgePolicy của dự án), không theo hôm nay, để các hội thoại lúc còn nhỏ không tự mở khi học sinh tròn 16.
/// Dùng đúng mốc và cách tính tuổi của dự án (AgeGroups.ChildMaxAgeExclusive, AgePolicy.GetAge) và cùng ngoại lệ: tài khoản do phụ huynh vận hành
/// không áp luật này. Tài khoản chưa khai ngày sinh coi như người lớn: đăng ký bắt buộc khai ngày sinh nên đó chủ yếu là tài khoản không qua
/// luồng học sinh (admin, moderator, trung tâm, tài khoản tạo tay).
/// </summary>
public static class AiQualityContentGate
{
    public static bool CanReadContent(AiQualityCandidate candidate, string minPolicyVersion = ConsentPurposes.QualityReviewMinPolicyVersion)
    {
        if (candidate.OwnerIsParentOperated || candidate.OwnerDateOfBirth is not { } birth)
            return true;
        var onTheDay = AgePolicy.TodayInVietnam(candidate.CreatedAt);
        if (AgePolicy.GetAge(birth, onTheDay) >= AgeGroups.ChildMaxAgeExclusive)
            return true;
        // Người dưới 16: cần sự đồng ý của phụ huynh với bản chính sách đủ mới
        return candidate.OwnerConsentVersions.Any(v => v is not null && string.CompareOrdinal(v, minPolicyVersion) >= 0);
    }
}
