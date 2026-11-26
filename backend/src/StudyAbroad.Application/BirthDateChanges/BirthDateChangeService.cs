using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.BirthDateChanges;

/// <summary>[UC-105 / UC-107] Chủ tài khoản xem, gửi và hủy yêu cầu sửa ngày sinh.</summary>
public interface IBirthDateChangeService
{
    /// <summary>Yêu cầu mới nhất của tài khoản (null khi chưa gửi lần nào).</summary>
    Task<BirthDateChangeResult<BirthDateChangeDto?>> GetCurrentAsync(Guid userId, CancellationToken ct = default);

    Task<BirthDateChangeResult<BirthDateChangeDto>> SubmitAsync(Guid userId, SubmitBirthDateChangeRequest request, CancellationToken ct = default);

    /// <summary>[UC-107 5a] Hủy khi còn Chờ duyệt; tệp minh chứng bị xóa.</summary>
    Task<BirthDateChangeResult<BirthDateChangeDto>> CancelAsync(Guid userId, Guid requestId, CancellationToken ct = default);
}

public class BirthDateChangeService(
    IBirthDateChangeRepository requests,
    IUserRepository users,
    IParentLinkRepository parentLinks,
    IBirthDateEvidenceStorage evidenceStorage,
    BirthDateChangeOptions? options = null,
    TimeProvider? clock = null) : IBirthDateChangeService
{
    public const int MaxReasonLength = 500;

    private readonly BirthDateChangeOptions _options = options ?? new BirthDateChangeOptions();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Tệp minh chứng chỉ nhận ảnh hoặc PDF, kiểm bằng chữ ký đầu tệp (không tin đuôi tệp hay content type trình duyệt gửi).</summary>
    private static readonly (byte[] Signature, string Extension, string ContentType)[] EvidenceSignatures =
    [
        ("%PDF"u8.ToArray(), ".pdf", "application/pdf"),
        ([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], ".png", "image/png"),
        ([0xFF, 0xD8, 0xFF], ".jpg", "image/jpeg"),
    ];

    public async Task<BirthDateChangeResult<BirthDateChangeDto?>> GetCurrentAsync(Guid userId, CancellationToken ct = default)
    {
        var latest = await requests.GetLatestByUserAsync(userId, ct);
        return BirthDateChangeResult<BirthDateChangeDto?>.Ok(latest is null ? null : ToDto(latest));
    }

    public async Task<BirthDateChangeResult<BirthDateChangeDto>> SubmitAsync(Guid userId, SubmitBirthDateChangeRequest request, CancellationToken ct = default)
    {
        var user = await users.GetUserById(userId, ct);
        // Con managed không phải tài khoản thật; trung tâm, kiểm duyệt, admin không có ngày sinh để sửa
        if (user is null || user.Status == UserStatuses.Managed)
            return Fail(BirthDateChangeError.NotFound, "Không tìm thấy tài khoản.");
        if (user.Role != UserRoles.Student && user.Role != UserRoles.Parent)
            return Fail(BirthDateChangeError.Forbidden, "Chỉ tài khoản Học sinh hoặc Phụ huynh được gửi yêu cầu sửa ngày sinh.");

        // E5: kiểm trước để báo kèm yêu cầu đang chờ; chỉ mục duy nhất chặn lần cuối khi bấm gửi hai lần cùng lúc
        if (await requests.GetPendingByUserAsync(userId, ct) is { } pending)
            return PendingExists(pending);

        // E1
        if (request.BirthDate is not { } newDob)
            return Fail(BirthDateChangeError.Validation, "Vui lòng nhập ngày sinh mới hợp lệ.");
        var today = AgePolicy.TodayInVietnam(_clock.GetUtcNow().UtcDateTime);
        if (newDob > today)
            return Fail(BirthDateChangeError.Validation, "Ngày sinh không được ở tương lai.");
        if (user.DateOfBirth == newDob)
            return Fail(BirthDateChangeError.Validation, "Ngày sinh mới trùng với ngày sinh hiện tại.");
        var newAge = AgePolicy.GetAge(newDob, today);
        if (newAge > AuthService.MaxAge)
            return Fail(BirthDateChangeError.Validation, $"Tuổi không được lớn hơn {AuthService.MaxAge}.");
        if (user.Role == UserRoles.Parent && newAge < AgeGroups.AdultAge)
            return Fail(BirthDateChangeError.Validation, $"Tài khoản phụ huynh phải từ {AgeGroups.AdultAge} tuổi.");
        if (user.Role == UserRoles.Student && newAge < AgeGroups.StudentMinAge)
            return Fail(BirthDateChangeError.Validation, $"Học sinh phải từ {AgeGroups.StudentMinAge} tuổi.");

        // E2
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (reason.Length == 0)
            return Fail(BirthDateChangeError.Validation, "Vui lòng nhập lý do sửa ngày sinh.");
        if (reason.Length > MaxReasonLength)
            return Fail(BirthDateChangeError.Validation, $"Lý do tối đa {MaxReasonLength} ký tự.");

        // 3a / E3: vượt mốc 16 hoặc 18 lên trên thì bắt buộc minh chứng.
        // Tài khoản cũ chưa có ngày sinh: không biết tuổi cũ nên coi như vượt mốc khi tuổi mới từ 16.
        int? oldAge = user.DateOfBirth is { } oldDob ? AgePolicy.GetAge(oldDob, today) : null;
        var evidenceRequired = CrossesUp(oldAge, newAge, AgeGroups.ChildMaxAgeExclusive) || CrossesUp(oldAge, newAge, AgeGroups.AdultAge);
        var evidence = request.Evidence is { Content.Length: > 0 } e ? e : null;
        if (evidence is null && evidenceRequired)
            return Fail(BirthDateChangeError.Validation,
                "Ngày sinh mới làm tuổi vượt mốc 16 hoặc 18, vui lòng tải lên minh chứng ngày sinh (giấy khai sinh, căn cước công dân, hộ chiếu hoặc thẻ học sinh).");

        string? evidenceType = null;
        (string Extension, string ContentType)? evidenceKind = null;
        if (evidence is not null)
        {
            evidenceType = request.EvidenceType?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(evidenceType) || !_options.AllowedEvidenceTypes.Contains(evidenceType))
                return Fail(BirthDateChangeError.Validation, "Vui lòng chọn loại giấy tờ minh chứng hợp lệ.");
            if (evidence.Content.Length > _options.MaxEvidenceBytes)
                return Fail(BirthDateChangeError.Validation, $"Tệp minh chứng tối đa {_options.MaxEvidenceBytes / (1024 * 1024)} MB.");
            evidenceKind = DetectEvidenceKind(evidence.Content);
            if (evidenceKind is null)
                return Fail(BirthDateChangeError.Validation, "Tệp minh chứng phải là ảnh (JPG, PNG) hoặc PDF.");
        }

        // 3b / E4: học sinh về dưới 16 mà chưa có đồng ý ở trạng thái Đã đồng ý thì cần email người đại diện
        string? representativeEmail = null;
        if (user.Role == UserRoles.Student && newAge < AgeGroups.ChildMaxAgeExclusive
            && !await parentLinks.HasGrantedConsentAsync(user.Id, ct))
        {
            representativeEmail = request.RepresentativeEmail?.Trim().ToLowerInvariant() ?? string.Empty;
            if (representativeEmail.Length == 0)
                return Fail(BirthDateChangeError.Validation, "Ngày sinh mới làm bạn dưới 16 tuổi, vui lòng nhập email người đại diện (cha mẹ hoặc người giám hộ).");
            if (!AuthService.IsValidEmail(representativeEmail))
                return Fail(BirthDateChangeError.Validation, "Email người đại diện không hợp lệ.");
            if (string.Equals(representativeEmail, user.Email, StringComparison.OrdinalIgnoreCase))
                return Fail(BirthDateChangeError.Validation, "Email người đại diện phải khác email của bạn.");
        }

        var entity = new BirthDateChangeRequest
        {
            UserId = user.Id,
            CurrentBirthDate = user.DateOfBirth,
            RequestedBirthDate = newDob,
            Reason = reason,
            Status = BirthDateChangeStatuses.Pending,
            RepresentativeEmail = representativeEmail,
        };

        if (evidence is not null && evidenceKind is { } kind)
        {
            entity.EvidenceType = evidenceType;
            entity.EvidenceFileName = TrimFileName(evidence.FileName, kind.Extension);
            entity.EvidenceContentType = kind.ContentType;
            entity.EvidenceSize = evidence.Content.Length;
            entity.EvidencePath = await evidenceStorage.SaveAsync(entity.Id, evidence.Content, kind.Extension, ct);
        }

        if (!await requests.TryAddAsync(entity, ct))
        {
            // Bấm gửi hai lần cùng lúc: lần sau đụng chỉ mục duy nhất, bỏ tệp vừa lưu
            evidenceStorage.Delete(entity.EvidencePath);
            var existing = await requests.GetPendingByUserAsync(userId, ct);
            return existing is null
                ? Fail(BirthDateChangeError.Conflict, "Bạn đã có yêu cầu sửa ngày sinh đang chờ duyệt.")
                : PendingExists(existing);
        }

        return BirthDateChangeResult<BirthDateChangeDto>.Ok(ToDto(entity), "Đã gửi yêu cầu sửa ngày sinh. Administrator sẽ xem xét yêu cầu của bạn.");
    }

    public async Task<BirthDateChangeResult<BirthDateChangeDto>> CancelAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var entity = await requests.GetByIdAsync(requestId, ct);
        // Chỉ chủ tài khoản: yêu cầu của người khác coi như không tồn tại
        if (entity is null || entity.UserId != userId)
            return Fail(BirthDateChangeError.NotFound, "Không tìm thấy yêu cầu sửa ngày sinh.");
        if (entity.Status != BirthDateChangeStatuses.Pending)
            return Fail(BirthDateChangeError.Conflict, "Chỉ hủy được yêu cầu đang chờ duyệt.", ToDto(entity));

        var now = _clock.GetUtcNow().UtcDateTime;
        if (!await requests.TryCloseAsync(entity.Id, BirthDateChangeStatuses.Cancelled, null, null, now, ct))
            return Fail(BirthDateChangeError.Conflict, "Yêu cầu vừa được Administrator xử lý, không còn hủy được.");

        evidenceStorage.Delete(entity.EvidencePath);
        entity.Status = BirthDateChangeStatuses.Cancelled;
        entity.EvidencePath = null;
        entity.EvidenceFileName = null;
        entity.EvidenceContentType = null;
        entity.EvidenceSize = null;
        entity.UpdatedAt = now;
        return BirthDateChangeResult<BirthDateChangeDto>.Ok(ToDto(entity), "Đã hủy yêu cầu sửa ngày sinh.");
    }

    /// <summary>Tuổi chuyển từ dưới mốc lên từ mốc trở lên. Tuổi cũ không rõ thì chỉ xét tuổi mới.</summary>
    internal static bool CrossesUp(int? oldAge, int newAge, int threshold) =>
        newAge >= threshold && (oldAge is null || oldAge < threshold);

    internal static (string Extension, string ContentType)? DetectEvidenceKind(byte[] content)
    {
        foreach (var (signature, extension, contentType) in EvidenceSignatures)
            if (content.AsSpan().StartsWith(signature))
                return (extension, contentType);
        return null;
    }

    private static string TrimFileName(string? fileName, string fallbackExtension)
    {
        var name = Path.GetFileName(fileName ?? string.Empty).Trim();
        if (name.Length == 0)
            name = "minh-chung" + fallbackExtension;
        return name.Length <= 256 ? name : name[^256..];
    }

    internal static BirthDateChangeDto ToDto(BirthDateChangeRequest r) => new(
        r.Id,
        r.CurrentBirthDate,
        r.RequestedBirthDate,
        r.Reason,
        r.Status,
        r.EvidenceType,
        r.EvidencePath is not null,
        r.RepresentativeEmail,
        r.RejectReason,
        r.CreatedAt,
        r.ReviewedAt);

    private static BirthDateChangeResult<BirthDateChangeDto> PendingExists(BirthDateChangeRequest pending) =>
        BirthDateChangeResult<BirthDateChangeDto>.Fail(
            BirthDateChangeError.Conflict,
            "Bạn đã có yêu cầu sửa ngày sinh đang chờ duyệt. Hãy chờ Administrator xử lý hoặc hủy yêu cầu đó trước khi gửi yêu cầu mới.",
            ToDto(pending));

    private static BirthDateChangeResult<BirthDateChangeDto> Fail(BirthDateChangeError error, string message, BirthDateChangeDto? value = null) =>
        BirthDateChangeResult<BirthDateChangeDto>.Fail(error, message, value);
}
