using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// A study-abroad consulting center listed on the platform (data comes from the team's market survey).
/// </summary>
public class StudyCenter : BaseEntity
{
    /// <summary>Survey code, e.g. CTY_012.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }

    /// <summary>Study levels the center advises on. Values from <see cref="Constants.StudyLevels"/>.</summary>
    public List<string> StudyLevels { get; set; } = [];

    /// <summary>Service keys from the survey catalog, e.g. "CHON_TRUONG", "LUYEN_PHONG_VAN".</summary>
    public List<string> Services { get; set; } = [];

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Description { get; set; }

    /// <summary>Thời điểm email liên hệ/đăng nhập của trung tâm được xác thực qua OTP (UC-91).</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>Thời điểm số điện thoại của trung tâm được xác thực qua SMS OTP (UC-91, BR-C3).</summary>
    public DateTime? PhoneVerifiedAt { get; set; }

    /// <summary>Thời điểm trung tâm đồng ý Điều khoản &amp; Quyền riêng tư (UC-64).</summary>
    public DateTime? TermsAgreedAt { get; set; }

    /// <summary>Thời điểm trung tâm đồng ý cam kết trung thực về thông tin &amp; phí (UC-64).</summary>
    public DateTime? CommitmentAgreedAt { get; set; }

    /// <summary>Họ tên người đăng ký tài khoản trung tâm (UC-64).</summary>
    public string? RegistrantName { get; set; }

    /// <summary>Chức vụ người đăng ký tài khoản trung tâm (UC-64).</summary>
    public string? RegistrantPosition { get; set; }

    /// <summary>Mức phí tối thiểu công bố (VNĐ) - do trung tâm công bố (BR-C9, UC-92).</summary>
    public decimal? FeeMin { get; set; }

    /// <summary>Mức phí tối đa công bố (VNĐ) - do trung tâm công bố (BR-C9, UC-92).</summary>
    public decimal? FeeMax { get; set; }

    /// <summary>Mô tả chi tiết gói phí hoặc dịch vụ đi kèm - do trung tâm công bố.</summary>
    public string? FeeDescription { get; set; }

    /// <summary>unverified | pending_review | verified | rejected | suspended. (UC-64, UC-71, UC-59).</summary>
    public string VerificationStatus { get; set; } = "unverified";
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }
    /// <summary>Ghi chú kiểm duyệt / hồ sơ pháp lý khi xác minh hoặc cập nhật.</summary>
    public string? VerificationNote { get; set; }

    /// <summary>Tài khoản vai trò center quản lý hồ sơ này (cổng trung tâm, backlog #41).</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>study_abroad | test_prep | hybrid</summary>
    public string CenterType { get; set; } = "study_abroad";

    public string? LogoUrl { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<string> GalleryImages { get; set; } = [];

    public string? District { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int? StaffCount { get; set; }
    public int? EstablishedYear { get; set; }
    public string? LicenseNumber { get; set; }
    /// <summary>Mã số thuế / Mã số doanh nghiệp (GCN ĐKDN).</summary>
    public string? TaxCode { get; set; }
    /// <summary>Người đại diện theo pháp luật / Giám đốc trung tâm.</summary>
    public string? LegalRepresentative { get; set; }
    /// <summary>Cơ quan cấp phép hoặc tiếp nhận thông báo hoạt động (VD: Sở GD&amp;ĐT Hà Nội).</summary>
    public string? LicenseIssuedBy { get; set; }
    /// <summary>Ngày cấp giấy phép hoặc ngày gửi thông báo hoạt động.</summary>
    public DateOnly? LicenseIssuedDate { get; set; }
    /// <summary>licensed (GCN trước 07/2026) | notified (thông báo hoạt động hậu kiểm từ 07/2026 theo NQ 23/2026/NQ-CP).</summary>
    public string LegalComplianceMode { get; set; } = "licensed";
    /// <summary>Số giấy phép / quyết định hoạt động đào tạo ngoại ngữ (áp dụng cho trung tâm test_prep hoặc hybrid).</summary>
    public string? TrainingLicenseNumber { get; set; }
    /// <summary>Số tư vấn viên có chứng chỉ nghiệp vụ tư vấn du học.</summary>
    public int? CertifiedCounselorCount { get; set; }
    /// <summary>Danh sách URL tài liệu pháp lý minh chứng (ĐKKD, giấy phép/thông báo Sở GD&amp;ĐT, PCCC...).</summary>
    public List<string> LegalDocumentUrls { get; set; } = [];
    public List<string> Accreditations { get; set; } = [];

    public double? VisaSuccessRate { get; set; }
    public decimal AverageRating { get; set; } = 5.0m;
    public int ReviewCount { get; set; } = 0;

    public DateOnly? SurveyedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
