namespace StudyAbroad.Domain.Constants;

public static class CenterDocumentTypes
{
    /// <summary>Scan / ảnh Giấy chứng nhận đăng ký doanh nghiệp (bắt buộc).</summary>
    public const string BusinessRegistration = "business_registration";

    /// <summary>Giấy ủy quyền nếu người đăng ký không phải người đại diện theo pháp luật (tùy chọn).</summary>
    public const string PowerOfAttorney = "power_of_attorney";

    /// <summary>Hợp đồng đại lý hoặc thư xác nhận nếu công bố đối tác của trường (tùy chọn).</summary>
    public const string PartnershipContract = "partnership_contract";

    /// <summary>Giấy chứng nhận đăng ký hoạt động tư vấn du học (tùy chọn).</summary>
    public const string ConsultingLicense = "consulting_license";
}

