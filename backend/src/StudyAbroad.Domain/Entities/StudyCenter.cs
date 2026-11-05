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

    /// <summary>unverified | verified | rejected | suspended. Admin xác minh ở backlog #25.</summary>
    public string VerificationStatus { get; set; } = "unverified";
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedByUserId { get; set; }

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
    public List<string> Accreditations { get; set; } = [];

    public double? VisaSuccessRate { get; set; }
    public decimal AverageRating { get; set; } = 5.0m;
    public int ReviewCount { get; set; } = 0;

    public DateOnly? SurveyedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
