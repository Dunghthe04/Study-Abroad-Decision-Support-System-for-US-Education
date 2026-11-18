using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Phụ huynh gợi ý một trường cho con. Chọn trường theo Id hoặc mã trường, lời nhắn không bắt buộc.</summary>
public record SuggestTargetSchoolRequest(Guid? UniversityId = null, string? UniversityCode = null, string? Note = null);

/// <summary>NotLinked → 404. Error → 400. Còn lại trả TargetSchool vừa gợi ý.</summary>
public record SuggestTargetSchoolResult(bool NotLinked, string? Error, TargetSchoolDto? TargetSchool)
{
    public static readonly SuggestTargetSchoolResult NoLink = new(true, null, null);
    public static SuggestTargetSchoolResult Fail(string error) => new(false, error, null);
    public static SuggestTargetSchoolResult Ok(TargetSchoolDto dto) => new(false, null, dto);
}

/// <summary>Khu "Con của tôi": phụ huynh xem dữ liệu của con đã liên kết, và gợi ý trường mục tiêu cho con.
/// Không có liên kết còn hiệu lực với con đó thì coi như không tìm thấy (controller trả 404), giống UC-86/87.</summary>
public class ParentChildrenService(IParentLinkRepository links, ITargetSchoolRepository targetSchools, IAuditLogRepository auditLogs)
{
    private const int MaxNoteLength = 1000;   // cùng giới hạn ghi chú với TargetSchoolService

    // Danh sách trường mục tiêu của con, gồm cả các gợi ý con chưa xem (Status parent_suggested).
    public async Task<IReadOnlyList<TargetSchoolDto>?> GetTargetSchoolsAsync(Guid parentId, Guid studentId, CancellationToken ct)
    {
        if (!await IsLinkedAsync(parentId, studentId, ct))
            return null;

        return await targetSchools.GetListByUserAsync(studentId, null, null, ct);
    }

    // Gợi ý: thêm một dòng parent_suggested vào danh sách của con. Con thêm (thành considering) hoặc bỏ qua (xóa dòng).
    public async Task<SuggestTargetSchoolResult> SuggestTargetSchoolAsync(
        Guid parentId, Guid studentId, SuggestTargetSchoolRequest request, CancellationToken ct)
    {
        if (!await IsLinkedAsync(parentId, studentId, ct))
            return SuggestTargetSchoolResult.NoLink;

        University? university = null;
        if (request.UniversityId is { } id && id != Guid.Empty)
            university = await targetSchools.GetUniversityAsync(id, ct);
        else if (!string.IsNullOrWhiteSpace(request.UniversityCode))
            university = await targetSchools.GetUniversityByCodeAsync(request.UniversityCode.Trim(), ct);
        if (university is null)
            return SuggestTargetSchoolResult.Fail("Trường đại học không tồn tại hoặc đã ngừng hoạt động trong hệ thống.");

        var existing = await targetSchools.GetByUserAndUniversityAsync(studentId, university.Id, ct);
        if (existing is not null)
            return SuggestTargetSchoolResult.Fail(TargetSchoolStatuses.IsParentSuggestion(existing.Status)
                ? "Bạn đã gợi ý trường này, đang chờ con xem."
                : "Con đã có trường này trong danh sách mục tiêu.");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        if (note?.Length > MaxNoteLength)
            note = note[..MaxNoteLength];

        var row = await targetSchools.AddAsync(new TargetSchool
        {
            UserId = studentId,
            UniversityId = university.Id,
            Status = TargetSchoolStatuses.ParentSuggested,
            Priority = 0,
            Notes = note,
        }, ct);

        await LogAsync(parentId, "target_school.parent_suggest", row, university.Name, ct);

        var dto = await targetSchools.GetDtoByIdAsync(row.Id, ct)
            ?? new TargetSchoolDto(row.Id, row.UserId, row.UniversityId, university.Code, university.Name, university.City,
                university.State, university.Website, university.Control, university.AcceptanceRate, row.Category, row.Status,
                row.Priority, row.Notes, row.ApplicationRound, row.Deadline, row.CreatedAt, row.UpdatedAt);
        return SuggestTargetSchoolResult.Ok(dto);
    }

    // Rút lại gợi ý con chưa xem. null = không liên kết (404), false = không có gợi ý đó (đã bị con thêm / bỏ qua).
    public async Task<bool?> WithdrawSuggestionAsync(Guid parentId, Guid studentId, Guid targetSchoolId, CancellationToken ct)
    {
        if (!await IsLinkedAsync(parentId, studentId, ct))
            return null;

        var row = await targetSchools.GetByIdAsync(targetSchoolId, ct);
        // Chỉ xóa được dòng gợi ý của đúng con này; trường con đã thêm thì phụ huynh không xóa được
        if (row is null || row.UserId != studentId || !TargetSchoolStatuses.IsParentSuggestion(row.Status))
            return false;

        await targetSchools.DeleteAsync(row, ct);
        await LogAsync(parentId, "target_school.parent_withdraw", row, null, ct);
        return true;
    }

    // Cùng cách kiểm tra với RecommendationHistoryService.CanViewAsync, nhưng không cho "xem của chính mình":
    // đây là khu của phụ huynh, chỉ xem con.
    private async Task<bool> IsLinkedAsync(Guid parentId, Guid studentId, CancellationToken ct) =>
        (await links.GetActiveByParentAsync(parentId, ct)).Any(l => l.StudentUserId == studentId);

    private Task LogAsync(Guid parentId, string action, TargetSchool row, string? universityName, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = parentId,
            Action = action,
            EntityType = "target_school",
            EntityId = row.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { studentUserId = row.UserId, universityId = row.UniversityId, universityName }),
        }, ct);
}
