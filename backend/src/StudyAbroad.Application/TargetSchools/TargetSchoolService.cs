using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.TargetSchools;

/// <summary>
/// Triển khai ITargetSchoolService xử lý nghiệp vụ cho trường mục tiêu (UC-30).
/// </summary>
public class TargetSchoolService(
    ITargetSchoolRepository repository,
    IAuditLogRepository auditLogs) : ITargetSchoolService
{
    private static readonly HashSet<string> ValidCategories = ["reach", "match", "safety"];

    public async Task<AddTargetSchoolResult> AddTargetSchoolAsync(Guid userId, AddTargetSchoolRequest request, CancellationToken ct = default)
    {
        if (request.UniversityId == Guid.Empty)
            return AddTargetSchoolResult.Fail("ID trường đại học không hợp lệ.");

        // 1. Kiểm tra trường đại học có tồn tại và đang hoạt động không
        var university = await repository.GetUniversityAsync(request.UniversityId, ct);
        if (university == null)
            return AddTargetSchoolResult.Fail("Trường đại học không tồn tại hoặc đã ngừng hoạt động trong hệ thống.");

        // 2. Kiểm tra xem trường đã có trong danh sách mục tiêu của học sinh chưa (UC-30 chặn thêm trùng)
        if (await repository.ExistsAsync(userId, request.UniversityId, ct))
            return AddTargetSchoolResult.Fail("Trường này đã có trong danh sách mục tiêu của bạn.");

        // 3. Chuẩn hóa Category (reach / match / safety)
        string? category = null;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var normalized = request.Category.Trim().ToLowerInvariant();
            if (!ValidCategories.Contains(normalized))
                return AddTargetSchoolResult.Fail("Phân loại trường mục tiêu phải là: reach (với), match (vừa sức), hoặc safety (an toàn).");
            category = normalized;
        }

        // 4. Xác định ApplicationRound và Deadline
        var round = string.IsNullOrWhiteSpace(request.ApplicationRound) ? null : request.ApplicationRound.Trim();
        var deadline = request.Deadline;

        // Nếu có chọn vòng nộp mà chưa có deadline, thử trích xuất deadline tương ứng từ DeadlinesJson của trường
        if (deadline == null && !string.IsNullOrEmpty(round))
        {
            var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(request.UniversityId, ct);
            if (!string.IsNullOrEmpty(deadlinesJson))
            {
                deadline = TryExtractDeadline(deadlinesJson, round);
            }
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes?.Length > 1000)
            notes = notes[..1000];

        var targetSchool = new TargetSchool
        {
            UserId = userId,
            UniversityId = request.UniversityId,
            Category = category,
            Status = "considering",
            Priority = 0,
            Notes = notes,
            ApplicationRound = round,
            Deadline = deadline,
        };

        await repository.AddAsync(targetSchool, ct);

        // Ghi Audit Log hành động thêm trường mục tiêu
        var detailJson = JsonSerializer.Serialize(new
        {
            userId,
            universityId = university.Id,
            universityName = university.Name,
            category = targetSchool.Category,
            round = targetSchool.ApplicationRound,
            deadline = targetSchool.Deadline,
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "target_school.add",
            EntityType = "target_school",
            EntityId = targetSchool.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        var dto = await repository.GetDtoByIdAsync(targetSchool.Id, ct)
            ?? ToDto(targetSchool, university);

        return AddTargetSchoolResult.Ok(dto);
    }

    public async Task<TargetSchoolDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var targetSchool = await repository.GetByIdAsync(id, ct);
        if (targetSchool == null || targetSchool.UserId != userId)
            return null;

        return await repository.GetDtoByIdAsync(id, ct);
    }

    private static DateTime? TryExtractDeadline(string json, string round)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var normalizedRound = round.ToUpperInvariant();
            string? dateStr = null;

            foreach (var prop in root.EnumerateObject())
            {
                var nameUpper = prop.Name.ToUpperInvariant();
                if (nameUpper == normalizedRound ||
                    (normalizedRound.Contains("EARLY DECISION") && nameUpper.Contains("ED")) ||
                    (normalizedRound.Contains("EARLY ACTION") && nameUpper.Contains("EA")) ||
                    (normalizedRound.Contains("REGULAR") && nameUpper.Contains("RD")))
                {
                    dateStr = prop.Value.GetString();
                    break;
                }
            }

            if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var parsedDate))
            {
                return DateTime.SpecifyKind(parsedDate, DateTimeKind.Utc);
            }
        }
        catch
        {
            // Bỏ qua lỗi cú pháp JSON nếu chuỗi deadlines không đúng định dạng
        }

        return null;
    }

    private static TargetSchoolDto ToDto(TargetSchool ts, University u) =>
        new(ts.Id, ts.UserId, ts.UniversityId, u.Code, u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate,
            ts.Category, ts.Status, ts.Priority, ts.Notes, ts.ApplicationRound, ts.Deadline, ts.CreatedAt, ts.UpdatedAt);
}
