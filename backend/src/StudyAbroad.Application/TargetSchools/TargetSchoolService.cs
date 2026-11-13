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
        // 1. Kiểm tra trường đại học có tồn tại và đang hoạt động không (qua ID hoặc Mã Code)
        University? university = null;
        if (request.UniversityId.HasValue && request.UniversityId.Value != Guid.Empty)
        {
            university = await repository.GetUniversityAsync(request.UniversityId.Value, ct);
        }
        else if (!string.IsNullOrWhiteSpace(request.UniversityCode))
        {
            university = await repository.GetUniversityByCodeAsync(request.UniversityCode.Trim(), ct);
        }

        if (university == null)
            return AddTargetSchoolResult.Fail("Trường đại học không tồn tại hoặc đã ngừng hoạt động trong hệ thống.");

        // 2. Kiểm tra xem trường đã có trong danh sách mục tiêu của học sinh chưa (UC-30 chặn thêm trùng)
        if (await repository.ExistsAsync(userId, university.Id, ct))
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
        DateTime? deadline = request.Deadline.HasValue
            ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc)
            : null;

        // Nếu có chọn vòng nộp mà chưa có deadline, thử trích xuất deadline tương ứng từ DeadlinesJson của trường
        if (deadline == null && !string.IsNullOrEmpty(round))
        {
            var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(university.Id, ct);
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
            UniversityId = university.Id,
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

    public async Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
    {
        var allItems = await repository.GetListByUserAsync(userId, null, null, ct);

        var reachCount = allItems.Count(x => string.Equals(x.Category, "reach", StringComparison.OrdinalIgnoreCase));
        var matchCount = allItems.Count(x => string.Equals(x.Category, "match", StringComparison.OrdinalIgnoreCase));
        var safetyCount = allItems.Count(x => string.Equals(x.Category, "safety", StringComparison.OrdinalIgnoreCase));
        var undecidedCount = allItems.Count(x => string.IsNullOrEmpty(x.Category));

        var now = DateTime.UtcNow;
        var nextDeadline = allItems
            .Where(x => x.Deadline.HasValue && x.Deadline.Value >= now)
            .OrderBy(x => x.Deadline!.Value)
            .Select(x => x.Deadline)
            .FirstOrDefault();

        var filteredItems = allItems;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToLowerInvariant();
            filteredItems = filteredItems.Where(x => string.Equals(x.Category, cat, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim().ToLowerInvariant();
            filteredItems = filteredItems.Where(x => string.Equals(x.Status, st, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new TargetSchoolSummaryDto(
            Items: filteredItems,
            TotalCount: allItems.Count,
            ReachCount: reachCount,
            MatchCount: matchCount,
            SafetyCount: safetyCount,
            UndecidedCategoryCount: undecidedCount,
            NextDeadline: nextDeadline
        );
    }

    public async Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(
        Guid userId,
        Guid targetSchoolId,
        UpdateApplicationRoundRequest request,
        CancellationToken ct = default)
    {
        // 1. Kiểm tra target school có tồn tại và thuộc về user không (Anti-IDOR)
        var targetSchool = await repository.GetByIdAsync(targetSchoolId, ct);
        if (targetSchool == null || targetSchool.UserId != userId)
            return UpdateApplicationRoundResult.Fail("Không tìm thấy trường mục tiêu trong danh sách của bạn.");

        var round = request.ApplicationRound?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(round))
            return UpdateApplicationRoundResult.Fail("Vui lòng chọn vòng nộp hồ sơ.");

        if (round.Length > 100)
            return UpdateApplicationRoundResult.Fail("Tên vòng nộp hồ sơ tối đa 100 ký tự.");

        // 2. Xác định deadline (tự động trích xuất từ DeadlinesJson nếu không truyền cụ thể)
        DateTime? deadline = request.Deadline.HasValue
            ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc)
            : null;

        if (deadline == null && request.AutoResolveDeadline)
        {
            var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(targetSchool.UniversityId, ct);
            if (!string.IsNullOrEmpty(deadlinesJson))
            {
                deadline = TryExtractDeadline(deadlinesJson, round);
            }
        }

        var previousRound = targetSchool.ApplicationRound;
        var previousDeadline = targetSchool.Deadline;

        targetSchool.ApplicationRound = round;
        targetSchool.Deadline = deadline;

        await repository.UpdateAsync(targetSchool, ct);

        // 3. Ghi Audit Log hành động cập nhật đợt nộp
        var detailJson = JsonSerializer.Serialize(new
        {
            userId,
            targetSchoolId,
            universityId = targetSchool.UniversityId,
            previousRound,
            newRound = targetSchool.ApplicationRound,
            previousDeadline,
            newDeadline = targetSchool.Deadline,
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "target_school.update_round",
            EntityType = "target_school",
            EntityId = targetSchool.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        var dto = await repository.GetDtoByIdAsync(targetSchool.Id, ct);
        return dto is not null
            ? UpdateApplicationRoundResult.Ok(dto)
            : UpdateApplicationRoundResult.Fail("Không thể tải thông tin sau khi cập nhật.");
    }

    public async Task<bool> RemoveTargetSchoolAsync(Guid userId, Guid targetSchoolId, CancellationToken ct = default)
    {
        var targetSchool = await repository.GetByIdAsync(targetSchoolId, ct);
        if (targetSchool == null || targetSchool.UserId != userId)
            return false;

        var university = await repository.GetUniversityAsync(targetSchool.UniversityId, ct);

        await repository.DeleteAsync(targetSchool, ct);

        // Ghi Audit Log hành động xóa trường mục tiêu
        var detailJson = JsonSerializer.Serialize(new
        {
            userId,
            universityId = targetSchool.UniversityId,
            universityName = university?.Name,
            universityCode = university?.Code,
            category = targetSchool.Category,
            round = targetSchool.ApplicationRound,
        });

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "target_school.remove",
            EntityType = "target_school",
            EntityId = targetSchool.Id.ToString(),
            DetailJson = detailJson,
        }, ct);

        return true;
    }

    public async Task<bool> RemoveTargetSchoolByUniversityAsync(Guid userId, string universityIdentifier, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(universityIdentifier))
            return false;

        var clean = universityIdentifier.Trim();
        TargetSchool? targetSchool = null;

        if (Guid.TryParse(clean, out var uniId))
        {
            targetSchool = await repository.GetByUserAndUniversityAsync(userId, uniId, ct);
        }

        targetSchool ??= await repository.GetByUserAndUniversityCodeAsync(userId, clean, ct);

        if (targetSchool == null || targetSchool.UserId != userId)
            return false;

        return await RemoveTargetSchoolAsync(userId, targetSchool.Id, ct);
    }

    public async Task<TargetSchoolDto?> CheckTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct = default)
    {
        if (universityId.HasValue && universityId.Value != Guid.Empty)
        {
            var targetSchool = await repository.GetByUserAndUniversityAsync(userId, universityId.Value, ct);
            if (targetSchool != null)
                return await repository.GetDtoByIdAsync(targetSchool.Id, ct);
        }

        if (!string.IsNullOrWhiteSpace(universityCode))
        {
            return await repository.GetDtoByUserAndUniversityCodeAsync(userId, universityCode.Trim(), ct);
        }

        return null;
    }

    private static DateTime? TryExtractDeadline(string json, string round)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var roundKey = CanonicalRound(round);
            string? dateStr = null;

            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.String) continue;
                if (string.Equals(prop.Name, round, StringComparison.OrdinalIgnoreCase) ||
                    (roundKey != null && CanonicalRound(prop.Name) == roundKey))
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

    /// <summary>
    /// Chuẩn hóa tên vòng nộp (ED/EA/RD/Rolling) theo từng từ, tránh khớp nhầm chuỗi con
    /// (vd. "REGULAR_DEADLINE" chứa "EA").
    /// </summary>
    private static string? CanonicalRound(string name)
    {
        var tokens = System.Text.RegularExpressions.Regex
            .Split(name.ToUpperInvariant(), "[^A-Z0-9]+")
            .Where(t => t.Length > 0)
            .ToHashSet();

        if (tokens.Contains("ED") || (tokens.Contains("EARLY") && tokens.Contains("DECISION"))) return "ED";
        if (tokens.Contains("EA") || (tokens.Contains("EARLY") && tokens.Contains("ACTION"))) return "EA";
        if (tokens.Contains("RD") || tokens.Contains("REGULAR")) return "RD";
        if (tokens.Contains("ROLLING")) return "ROLLING";
        return null;
    }

    private static TargetSchoolDto ToDto(TargetSchool ts, University u) =>
        new(ts.Id, ts.UserId, ts.UniversityId, u.Code, u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate,
            ts.Category, ts.Status, ts.Priority, ts.Notes, ts.ApplicationRound, ts.Deadline, ts.CreatedAt, ts.UpdatedAt);
}
