using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

public record AddManagedChildRequest(string? FullName, DateOnly? DateOfBirth);

public record RenameManagedChildRequest(string? FullName);

public record ManagedChildDto(Guid StudentUserId, string FullName, DateOnly? DateOfBirth);

/// <summary>NotFound → 404 (không phải con managed của phụ huynh này). Error → 400. Còn lại trả con vừa thêm / sửa.</summary>
public record ManagedChildResult(bool NotFound, string? Error, ManagedChildDto? Child)
{
    public static readonly ManagedChildResult Missing = new(true, null, null);
    public static ManagedChildResult Fail(string error) => new(false, error, null);
    public static ManagedChildResult Ok(ManagedChildDto dto) => new(false, null, dto);
}

/// <summary>1.5a: phụ huynh thêm con chưa có tài khoản ("con managed"), đổi tên, xóa.
/// Con managed là một user riêng (Role student, Status managed, không mật khẩu) nối với phụ huynh bằng một liên kết active,
/// nên các chỗ đang đọc "con đã liên kết" (Con của tôi, gợi ý, kịch bản) dùng lại được.</summary>
public class ManagedChildService(
    IUserRepository users,
    IParentLinkRepository links,
    IUnitOfWork unitOfWork,
    IAuditLogRepository auditLogs)
{
    public const int MaxManagedChildren = 5;
    private const int MaxNameLength = 128;   // bằng giới hạn cột users.full_name
    private const int MaxAge = 100;          // như lúc đăng ký (AuthService.ValidateAge)
    public const string EmailDomain = "noreply.usas.local";

    public async Task<ManagedChildResult> AddAsync(Guid parentId, AddManagedChildRequest request, CancellationToken ct)
    {
        var name = NormalizeName(request.FullName);
        var error = ValidateName(name) ?? ValidateDateOfBirth(request.DateOfBirth);
        if (error is not null)
            return ManagedChildResult.Fail(error);

        var parent = await users.GetUserById(parentId, ct);
        if (parent is null || parent.Role != UserRoles.Parent)
            return ManagedChildResult.Fail("Chỉ phụ huynh mới thêm được con.");

        if ((await GetManagedChildrenAsync(parentId, ct)).Count >= MaxManagedChildren)
            return ManagedChildResult.Fail($"Bạn chỉ thêm được tối đa {MaxManagedChildren} con.");

        var now = DateTime.UtcNow;
        var child = new User
        {
            Email = $"managed-{Guid.NewGuid():N}@{EmailDomain}",   // cột Email bắt buộc và unique, không ai nhận thư ở đây
            PasswordHash = string.Empty,
            FullName = name,
            Role = UserRoles.Student,
            Status = UserStatuses.Managed,
            DateOfBirth = request.DateOfBirth,
        };
        var link = new ParentStudentLink
        {
            StudentUserId = child.Id,
            ParentUserId = parentId,
            ParentEmail = parent.Email.ToLowerInvariant(),
            Status = ParentLinkStatuses.Active,
            AcceptedAt = now,
        };

        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            await users.AddAsync(child, txCt);
            await links.AddAsync(link, txCt);
            await LogAsync(parentId, "parent.managed_child.add", child.Id, txCt);
        }, ct);

        return ManagedChildResult.Ok(ToDto(child));
    }

    public async Task<ManagedChildResult> RenameAsync(Guid parentId, Guid childId, RenameManagedChildRequest request, CancellationToken ct)
    {
        var name = NormalizeName(request.FullName);
        var error = ValidateName(name);
        if (error is not null)
            return ManagedChildResult.Fail(error);

        var found = await FindManagedChildAsync(parentId, childId, ct);
        if (found is null)
            return ManagedChildResult.Missing;

        var child = found.Value.Child;
        if (child.FullName != name)
        {
            child.FullName = name;
            child.UpdatedAt = DateTime.UtcNow;
            await users.UpdateUserAsync(child, ct);
            await LogAsync(parentId, "parent.managed_child.rename", child.Id, ct);
        }

        return ManagedChildResult.Ok(ToDto(child));
    }

    /// <summary>Xóa mềm con managed và thu hồi liên kết. Job dọn tài khoản UC-80 xóa hẳn sau 30 ngày.
    /// false = không phải con managed của phụ huynh này (404).</summary>
    public async Task<bool> DeleteAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var found = await FindManagedChildAsync(parentId, childId, ct);
        if (found is null)
            return false;

        var (child, link) = found.Value;
        var now = DateTime.UtcNow;

        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            child.Status = UserStatuses.Deleted;
            child.DeletedAt = now;
            child.UpdatedAt = now;
            await users.UpdateUserAsync(child, txCt);

            // Phiên làm thay (1.5b) đang mở thì hết hiệu lực luôn
            await users.RevokeAllSessionsAsync(child.Id, txCt);

            link.Status = ParentLinkStatuses.Revoked;
            link.RevokedAt = now;
            link.RevokedByUserId = parentId;
            link.UpdatedAt = now;
            await links.UpdateAsync(link, txCt);

            await LogAsync(parentId, "parent.managed_child.delete", child.Id, txCt);
        }, ct);

        return true;
    }

    // Các con managed còn hiệu lực của phụ huynh (liên kết active tới user Status managed). Con thật không tính.
    private async Task<List<User>> GetManagedChildrenAsync(Guid parentId, CancellationToken ct)
    {
        var result = new List<User>();
        foreach (var link in await links.GetActiveByParentAsync(parentId, ct))
        {
            var user = await users.GetUserById(link.StudentUserId, ct);
            if (user?.Status == UserStatuses.Managed)
                result.Add(user);
        }
        return result;
    }

    // Đủ 3 điều kiện: liên kết active, của đúng phụ huynh này, con có Status managed. Thiếu một là coi như không có.
    private async Task<(User Child, ParentStudentLink Link)?> FindManagedChildAsync(Guid parentId, Guid childId, CancellationToken ct)
    {
        var link = (await links.GetActiveByParentAsync(parentId, ct)).FirstOrDefault(l => l.StudentUserId == childId);
        if (link is null)
            return null;

        var child = await users.GetUserById(childId, ct);
        return child?.Status == UserStatuses.Managed ? (child, link) : null;
    }

    private static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    private static string? ValidateName(string name) =>
        name.Length == 0 ? "Vui lòng nhập tên của con."
        : name.Length > MaxNameLength ? $"Tên của con tối đa {MaxNameLength} ký tự."
        : null;

    private static string? ValidateDateOfBirth(DateOnly? dob)
    {
        if (dob is not { } value)
            return "Vui lòng nhập ngày sinh của con.";

        var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
        if (value > today)
            return "Ngày sinh không được ở tương lai.";

        return AgePolicy.GetAge(value, today) > MaxAge ? "Ngày sinh không hợp lệ." : null;
    }

    private static ManagedChildDto ToDto(User child) => new(child.Id, child.FullName, child.DateOfBirth);

    // Không ghi tên con vào log, chỉ id
    private Task LogAsync(Guid parentId, string action, Guid childId, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = parentId,
            Action = action,
            EntityType = "user",
            EntityId = childId.ToString(),
            DetailJson = JsonSerializer.Serialize(new { managedChildId = childId }),
        }, ct);
}
