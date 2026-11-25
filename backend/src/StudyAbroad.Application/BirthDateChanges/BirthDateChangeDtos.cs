namespace StudyAbroad.Application.BirthDateChanges;

public enum BirthDateChangeError
{
    None,
    Validation,
    NotFound,
    Forbidden,
    /// <summary>UC-107 E5: đã có yêu cầu Chờ duyệt. UC-150 E1: yêu cầu không còn Chờ duyệt.</summary>
    Conflict,
}

/// <summary>Tệp minh chứng người dùng tải lên (controller đọc từ multipart).</summary>
public record BirthDateEvidenceUpload(byte[] Content, string FileName);

/// <summary>[UC-107] Dữ liệu gửi yêu cầu sửa ngày sinh.</summary>
public record SubmitBirthDateChangeRequest(
    DateOnly? BirthDate,
    string? Reason,
    string? EvidenceType,
    BirthDateEvidenceUpload? Evidence,
    string? RepresentativeEmail);

/// <summary>[UC-105 / UC-107] Yêu cầu sửa ngày sinh hiện cho chủ tài khoản.</summary>
public record BirthDateChangeDto(
    Guid Id,
    DateOnly? CurrentBirthDate,
    DateOnly RequestedBirthDate,
    string Reason,
    string Status,
    string? EvidenceType,
    bool HasEvidence,
    string? RepresentativeEmail,
    string? RejectReason,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

public record BirthDateChangeResult<T>(BirthDateChangeError Error, string Message, T? Value)
{
    public bool Success => Error == BirthDateChangeError.None;
    public static BirthDateChangeResult<T> Ok(T value, string message = "") => new(BirthDateChangeError.None, message, value);
    public static BirthDateChangeResult<T> Fail(BirthDateChangeError error, string message, T? value = default) => new(error, message, value);
}
