using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.TargetSchools;

/// <summary>
/// Triển khai ITargetSchoolService xử lý nghiệp vụ cho trường mục tiêu (UC-30).
/// </summary>
public class TargetSchoolService(
    ITargetSchoolRepository repository,
    IAuditLogRepository auditLogs,
    IRecommendationHistoryRepository? recommendationHistory = null) : ITargetSchoolService
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

        // 2. Kiểm tra xem trường đã có trong danh sách mục tiêu của học sinh chưa (UC-30 chặn thêm trùng).
        // Dòng phụ huynh gợi ý chưa phải trường của con: con thêm trường đó là nhận gợi ý, dùng lại dòng đó.
        var existing = await repository.GetByUserAndUniversityAsync(userId, university.Id, ct);
        if (existing is not null && !TargetSchoolStatuses.IsParentSuggestion(existing.Status))
            return AddTargetSchoolResult.Fail("Trường này đã có trong danh sách mục tiêu của bạn.");

        // 3. Chuẩn hóa Category (reach / match / safety); mặc định lấy theo lần gợi ý gần nhất (SC-34 / UC-33)
        var recommendedCategory = await repository.GetLatestRecommendedCategoryAsync(userId, university.Id, ct);
        string? category = null;
        bool isCategoryUserSelected = false;
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            var normalized = request.Category.Trim().ToLowerInvariant();
            if (!ValidCategories.Contains(normalized))
                return AddTargetSchoolResult.Fail("Phân loại trường mục tiêu phải là: reach (với), match (vừa sức), hoặc safety (an toàn).");
            category = normalized;
            isCategoryUserSelected = request.IsUserCategory
                ?? !string.Equals(category, recommendedCategory, StringComparison.OrdinalIgnoreCase);
        }
        else if (!string.IsNullOrWhiteSpace(recommendedCategory))
        {
            category = recommendedCategory;
            isCategoryUserSelected = false;
        }
        else if (recommendationHistory is not null)
        {
            try
            {
                var recs = await recommendationHistory.ListByUserAsync(userId, 1, ct);
                var latest = recs.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(latest?.ItemsJson))
                {
                    using var doc = JsonDocument.Parse(latest.ItemsJson);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var group in new[] { "reach", "match", "safety" })
                        {
                            if (root.TryGetProperty(group, out var groupProp) && groupProp.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var elem in groupProp.EnumerateArray())
                                {
                                    var matchCode = elem.TryGetProperty("code", out var cp) && string.Equals(cp.GetString(), university.Code, StringComparison.OrdinalIgnoreCase);
                                    var matchId = elem.TryGetProperty("universityId", out var ip) && ip.TryGetGuid(out var gid) && gid == university.Id;
                                    if (matchCode || matchId)
                                    {
                                        category = group;
                                        break;
                                    }
                                }
                            }
                            if (category is not null) break;
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var elem in root.EnumerateArray())
                        {
                            var matchCode = elem.TryGetProperty("code", out var cp) && string.Equals(cp.GetString(), university.Code, StringComparison.OrdinalIgnoreCase);
                            var matchId = elem.TryGetProperty("universityId", out var ip) && ip.TryGetGuid(out var gid) && gid == university.Id;
                            if (matchCode || matchId)
                            {
                                if (elem.TryGetProperty("category", out var catProp) && catProp.GetString() is { } catStr)
                                {
                                    var normalized = catStr.Trim().ToLowerInvariant();
                                    if (ValidCategories.Contains(normalized)) category = normalized;
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Không chặn luồng nếu đọc lịch sử gợi ý gặp lỗi
            }
        }

        // 4. Xác định ApplicationRound và Deadline (kiểm tra trường có hỗ trợ vòng nộp & quy tắc tối đa 1 ED / 1 ED II - UC-32)
        var round = string.IsNullOrWhiteSpace(request.ApplicationRound) ? null : request.ApplicationRound.Trim();
        DateTime? deadline = request.Deadline.HasValue
            ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc)
            : null;

        if (!string.IsNullOrEmpty(round))
        {
            var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(university.Id, ct);
            if (!IsRoundSupported(deadlinesJson, round))
                return AddTargetSchoolResult.Fail($"Trường không hỗ trợ vòng nộp {round}.");

            var bindingError = await CheckExclusiveBindingRoundAsync(userId, round, existing?.Id, ct);
            if (bindingError is not null)
                return AddTargetSchoolResult.Fail(bindingError);

            if (deadline == null && !string.IsNullOrEmpty(deadlinesJson))
                deadline = TryExtractDeadline(deadlinesJson, round);
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        if (notes?.Length > 1000)
            notes = notes[..1000];

        TargetSchool targetSchool;
        if (existing is not null)
        {
            // Nhận gợi ý của phụ huynh: thành trường của con. Không ghi chú mới thì giữ lời nhắn của phụ huynh.
            existing.Category = category;
            existing.IsCategoryUserSelected = isCategoryUserSelected;
            existing.Status = TargetSchoolStatuses.Considering;
            existing.Notes = notes ?? existing.Notes;
            existing.ApplicationRound = round;
            existing.Deadline = deadline;
            await repository.UpdateAsync(existing, ct);
            targetSchool = existing;
        }
        else
        {
            targetSchool = new TargetSchool
            {
                UserId = userId,
                UniversityId = university.Id,
                Category = category,
                IsCategoryUserSelected = isCategoryUserSelected,
                Status = TargetSchoolStatuses.Considering,
                Priority = 0,
                Notes = notes,
                ApplicationRound = round,
                Deadline = deadline,
            };

            await repository.AddAsync(targetSchool, ct);
        }

        // Ghi Audit Log hành động thêm trường mục tiêu
        var detailJson = JsonSerializer.Serialize(new
        {
            userId,
            universityId = university.Id,
            universityName = university.Name,
            category = targetSchool.Category,
            round = targetSchool.ApplicationRound,
            deadline = targetSchool.Deadline,
            fromParentSuggestion = existing is not null,
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

        var dto = await repository.GetDtoByIdAsync(id, ct);
        if (dto is null) return null;
        var recCategories = await repository.GetLatestRecommendedCategoriesAsync(userId, ct);
        return ApplyRecommendedCategory(dto, recCategories);
    }

    public async Task<TargetSchoolSummaryDto> GetTargetSchoolsAsync(Guid userId, string? category = null, string? status = null, CancellationToken ct = default)
    {
        var rawRows = await repository.GetListByUserAsync(userId, null, null, ct);
        var recCategories = await repository.GetLatestRecommendedCategoriesAsync(userId, ct);
        var rows = rawRows.Select(r => ApplyRecommendedCategory(r, recCategories)).ToList();

        // Gợi ý của phụ huynh chưa phải trường của con: không đếm, không vào danh sách chính.
        // Chỉ trả khi hỏi đúng status=parent_suggested (mục "Phụ huynh gợi ý").
        var allItems = rows.Where(x => !TargetSchoolStatuses.IsParentSuggestion(x.Status)).ToList();
        var wantSuggestions = TargetSchoolStatuses.IsParentSuggestion(status?.Trim());

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

        var filteredItems = wantSuggestions
            ? rows.Where(x => TargetSchoolStatuses.IsParentSuggestion(x.Status)).ToList()
            : allItems;
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

        var scholarshipNeed = await repository.GetUserScholarshipNeedAsync(userId, ct);
        var isScholarshipMandatory = string.Equals(scholarshipNeed, ProfileConstants.ScholarshipNeeds.Mandatory, StringComparison.OrdinalIgnoreCase);

        return new TargetSchoolSummaryDto(
            Items: filteredItems,
            TotalCount: allItems.Count,
            ReachCount: reachCount,
            MatchCount: matchCount,
            SafetyCount: safetyCount,
            UndecidedCategoryCount: undecidedCount,
            NextDeadline: nextDeadline,
            IsScholarshipMandatory: isScholarshipMandatory
        );
    }

    public async Task<UpdateApplicationRoundResult> UpdateApplicationRoundAsync(
        Guid userId,
        Guid targetSchoolId,
        UpdateApplicationRoundRequest request,
        CancellationToken ct = default)
    {
        // 1. Kiểm tra target school có tồn tại và thuộc về user không (Anti-IDOR, UC-32 trả 404)
        var targetSchool = await repository.GetByIdAsync(targetSchoolId, ct);
        if (targetSchool == null || targetSchool.UserId != userId)
            return UpdateApplicationRoundResult.NotFoundError("Không tìm thấy trường mục tiêu trong danh sách của bạn.");

        var round = request.ApplicationRound?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(round))
            return UpdateApplicationRoundResult.Fail("Vui lòng chọn vòng nộp hồ sơ.");

        if (round.Length > 100)
            return UpdateApplicationRoundResult.Fail("Tên vòng nộp hồ sơ tối đa 100 ký tự.");

        // 2. Kiểm tra trường có hỗ trợ vòng nộp này không (UC-32), quy tắc tối đa 1 ED / 1 ED II (E1) và tự trích xuất deadline
        var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(targetSchool.UniversityId, ct);
        if (!IsRoundSupported(deadlinesJson, round))
            return UpdateApplicationRoundResult.Fail($"Trường không hỗ trợ vòng nộp {round}.");

        var bindingError = await CheckExclusiveBindingRoundAsync(userId, round, targetSchool.Id, ct);
        if (bindingError is not null)
            return UpdateApplicationRoundResult.Fail(bindingError);

        DateTime? deadline = request.Deadline.HasValue
            ? DateTime.SpecifyKind(request.Deadline.Value, DateTimeKind.Utc)
            : null;

        if (deadline == null && request.AutoResolveDeadline && !string.IsNullOrEmpty(deadlinesJson))
        {
            deadline = TryExtractDeadline(deadlinesJson, round);
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
        if (dto is null)
            return UpdateApplicationRoundResult.Fail("Không thể tải thông tin sau khi cập nhật.");

        var recCategories = await repository.GetLatestRecommendedCategoriesAsync(userId, ct);
        return UpdateApplicationRoundResult.Ok(ApplyRecommendedCategory(dto, recCategories));
    }

    public async Task<UpdateTargetCategoryResult> UpdateCategoryAsync(
        Guid userId,
        Guid targetSchoolId,
        UpdateTargetCategoryRequest request,
        CancellationToken ct = default)
    {
        var targetSchool = await repository.GetByIdAsync(targetSchoolId, ct);
        if (targetSchool == null || targetSchool.UserId != userId)
            return UpdateTargetCategoryResult.NotFoundError("Không tìm thấy trường mục tiêu trong danh sách của bạn.");

        var normalized = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!ValidCategories.Contains(normalized))
            return UpdateTargetCategoryResult.Fail("Phân loại trường mục tiêu phải là: reach (với), match (vừa sức), hoặc safety (an toàn).");

        var previousCategory = targetSchool.Category;
        targetSchool.Category = normalized;
        targetSchool.IsCategoryUserSelected = true;

        await repository.UpdateAsync(targetSchool, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = userId,
            Action = "target_school.update_category",
            EntityType = "target_school",
            EntityId = targetSchool.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                userId,
                targetSchoolId,
                universityId = targetSchool.UniversityId,
                previousCategory,
                newCategory = normalized,
                isCategoryUserSelected = true,
            }),
        }, ct);

        var dto = await repository.GetDtoByIdAsync(targetSchool.Id, ct);
        return dto is not null
            ? UpdateTargetCategoryResult.Ok(dto)
            : UpdateTargetCategoryResult.Fail("Không thể tải thông tin sau khi cập nhật.");
    }

    public async Task<TargetSchoolDefaultsDto> GetDefaultsAsync(
        Guid userId,
        string? universityCode,
        Guid? universityId,
        CancellationToken ct = default)
    {
        University? university = null;
        if (universityId.HasValue && universityId.Value != Guid.Empty)
            university = await repository.GetUniversityAsync(universityId.Value, ct);
        else if (!string.IsNullOrWhiteSpace(universityCode))
            university = await repository.GetUniversityByCodeAsync(universityCode.Trim(), ct);

        if (university is null)
            return new TargetSchoolDefaultsDto(null, []);

        var recommendedCategory = await repository.GetLatestRecommendedCategoryAsync(userId, university.Id, ct);
        var deadlinesJson = await repository.GetUniversityDeadlinesJsonAsync(university.Id, ct);
        var scholarshipNeed = await repository.GetUserScholarshipNeedAsync(userId, ct);
        var isScholarshipMandatory = string.Equals(scholarshipNeed, ProfileConstants.ScholarshipNeeds.Mandatory, StringComparison.OrdinalIgnoreCase);
        var existingEd = await repository.FindSchoolWithRoundAsync(userId, "ED", null, ct);
        var existingEd2 = await repository.FindSchoolWithRoundAsync(userId, "ED2", null, ct);

        return new TargetSchoolDefaultsDto(
            recommendedCategory,
            ExtractAvailableRounds(deadlinesJson),
            isScholarshipMandatory,
            existingEd,
            existingEd2);
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
        // Trường phụ huynh mới gợi ý thì coi như con chưa có: trang trường vẫn hiện nút Thêm (thêm = nhận gợi ý)
        var dto = await FindTargetSchoolAsync(userId, universityCode, universityId, ct);
        if (dto is null || TargetSchoolStatuses.IsParentSuggestion(dto.Status))
            return null;

        var recCategories = await repository.GetLatestRecommendedCategoriesAsync(userId, ct);
        return ApplyRecommendedCategory(dto, recCategories);
    }

    private async Task<TargetSchoolDto?> FindTargetSchoolAsync(Guid userId, string? universityCode, Guid? universityId, CancellationToken ct)
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
    /// Chuẩn hóa tên vòng nộp (ED/ED2/EA/REA/RD/Rolling) theo từng từ, tránh khớp nhầm chuỗi con
    /// (vd. "REGULAR_DEADLINE" chứa "EA", "Early Decision II" không gộp vào "ED").
    /// </summary>
    public static string? CanonicalRound(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var tokens = System.Text.RegularExpressions.Regex
            .Split(name.ToUpperInvariant(), "[^A-Z0-9]+")
            .Where(t => t.Length > 0)
            .ToHashSet();

        if (tokens.Contains("ED2") || tokens.Contains("EDII") ||
            (tokens.Contains("EARLY") && tokens.Contains("DECISION") && (tokens.Contains("2") || tokens.Contains("II"))) ||
            (tokens.Contains("ED") && (tokens.Contains("2") || tokens.Contains("II"))))
            return "ED2";

        if (tokens.Contains("ED") || tokens.Contains("ED1") || tokens.Contains("EDI") ||
            (tokens.Contains("EARLY") && tokens.Contains("DECISION")))
            return "ED";

        if (tokens.Contains("REA") || tokens.Contains("SCEA") ||
            (tokens.Contains("RESTRICTIVE") && tokens.Contains("EARLY") && tokens.Contains("ACTION")) ||
            (tokens.Contains("SINGLE") && tokens.Contains("CHOICE") && tokens.Contains("EARLY") && tokens.Contains("ACTION")))
            return "REA";

        if (tokens.Contains("EA") || tokens.Contains("EA1") || tokens.Contains("EA2") ||
            (tokens.Contains("EARLY") && tokens.Contains("ACTION")))
            return "EA";

        if (tokens.Contains("RD") || tokens.Contains("REGULAR")) return "RD";
        if (tokens.Any(t => t == "ROLLING" || t.StartsWith("ROLLING", StringComparison.Ordinal))) return "ROLLING";
        return null;
    }

    /// <summary>
    /// Trích xuất danh sách vòng nộp trường hỗ trợ từ deadlines_json (theo thứ tự UC-32: ED, ED II, EA, REA, RD, Rolling).
    /// </summary>
    public static IReadOnlyList<string> ExtractAvailableRounds(string? deadlinesJson)
    {
        if (string.IsNullOrWhiteSpace(deadlinesJson))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(deadlinesJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return [];

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) continue;
                if (prop.Value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(prop.Value.GetString())) continue;
                var canonical = CanonicalRound(prop.Name);
                if (canonical is not null)
                    keys.Add(canonical);
            }

            var ordered = new List<string>(6);
            if (keys.Contains("ED")) ordered.Add("Early Decision");
            if (keys.Contains("ED2")) ordered.Add("Early Decision II");
            if (keys.Contains("EA")) ordered.Add("Early Action");
            if (keys.Contains("REA")) ordered.Add("Restrictive Early Action");
            if (keys.Contains("RD")) ordered.Add("Regular Decision");
            if (keys.Contains("ROLLING")) ordered.Add("Rolling");
            return ordered;
        }
        catch
        {
            return [];
        }
    }

    private static bool IsRoundSupported(string? deadlinesJson, string round)
    {
        var roundKey = CanonicalRound(round);
        if (roundKey is null)
            return false;

        if (string.IsNullOrWhiteSpace(deadlinesJson))
            return true;

        var available = ExtractAvailableRounds(deadlinesJson);
        if (available.Count == 0)
            return true;

        return available.Any(r => CanonicalRound(r) == roundKey);
    }

    /// <summary>
    /// [UC-32 E1] Mỗi học sinh chỉ có tối đa một trường ở Early Decision và một trường ở Early Decision II tại một thời điểm.
    /// </summary>
    private async Task<string?> CheckExclusiveBindingRoundAsync(
        Guid userId,
        string round,
        Guid? excludeTargetId,
        CancellationToken ct)
    {
        var canonical = CanonicalRound(round);
        if (canonical is not ("ED" or "ED2"))
            return null;

        var conflictSchool = await repository.FindSchoolWithRoundAsync(userId, canonical, excludeTargetId, ct);
        if (string.IsNullOrWhiteSpace(conflictSchool))
            return null;

        var roundLabel = canonical == "ED2" ? "Early Decision II" : "Early Decision";
        return $"Bạn đã chọn vòng {roundLabel} cho {conflictSchool}. Mỗi học sinh chỉ được chọn tối đa một trường ở vòng {roundLabel}.";
    }

    private static TargetSchoolDto ApplyRecommendedCategory(TargetSchoolDto dto, IReadOnlyDictionary<Guid, string> recCategories) =>
        !dto.IsCategoryUserSelected && recCategories.TryGetValue(dto.UniversityId, out var recCat)
            ? dto with { Category = recCat }
            : dto;

    private static TargetSchoolDto ToDto(TargetSchool ts, University u) =>
        new(ts.Id, ts.UserId, ts.UniversityId, u.Code, u.Name, u.City, u.State, u.Website, u.Control, u.AcceptanceRate,
            ts.Category, ts.Status, ts.Priority, ts.Notes, ts.ApplicationRound, ts.Deadline, ts.CreatedAt, ts.UpdatedAt,
            IsCategoryUserSelected: ts.IsCategoryUserSelected);
}
