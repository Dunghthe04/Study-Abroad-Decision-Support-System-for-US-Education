namespace StudyAbroad.Domain.Constants;

/// <summary>[UC-107 / UC-150] Trạng thái yêu cầu sửa ngày sinh (birth_date_change_requests.status).</summary>
public static class BirthDateChangeStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
}

/// <summary>[UC-107 3a] Loại giấy tờ minh chứng khi ngày sinh mới vượt mốc 16 / 18 tuổi.</summary>
public static class BirthDateEvidenceTypes
{
    public const string BirthCertificate = "birth_certificate";
    public const string CitizenId = "citizen_id";
    public const string Passport = "passport";
    public const string StudentCard = "student_card";

    public static readonly IReadOnlyList<string> All = [BirthCertificate, CitizenId, Passport, StudentCard];
}
