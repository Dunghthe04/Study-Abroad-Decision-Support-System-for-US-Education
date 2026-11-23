using StudyAbroad.Application.Common;
using StudyAbroad.Application.Universities;

namespace StudyAbroad.Application.Scholarships;

/// <summary>[USAS-2030] Bộ lọc tìm học bổng công khai (UC-29).</summary>
public record ScholarshipSearchQuery(
    string? Search = null,
    string? StudyLevel = null,
    decimal? MinAmountUsd = null,
    decimal? MaxAmountUsd = null,
    string? CoverageType = null,
    decimal? MaxMinGpa4 = null,
    decimal? MaxMinIelts = null,
    /// <summary>chỉ lấy học bổng còn hạn từ ngày này; hạn null (xét quanh năm) luôn được giữ</summary>
    DateOnly? DeadlineFrom = null,
    int Page = 1,
    int PageSize = 20,
    /// <summary>loại học bổng: merit | need | merit_and_need | athletic | program | external | other</summary>
    string? Kind = null,
    /// <summary>true: chỉ học bổng mở cho người Việt (chưa rõ thì bị loại)</summary>
    bool? VnEligible = null);

/// <summary>Không có cột quản trị (người sửa, trạng thái ẩn).</summary>
public record ScholarshipDto(
    Guid Id,
    string? UniversityCode,
    string? UniversityName,
    string Name,
    string? Provider,
    string StudyLevel,
    decimal? AmountUsd,
    string? CoverageType,
    decimal? MinGpa4,
    decimal? MinIelts,
    DateOnly? Deadline,
    string? EligibilityNotes,
    string? SourceUrl,
    DateOnly? RetrievedAt,
    string? SourceReliability,
    string? Kind = null,
    string? AmountType = null,
    decimal? AmountMinUsd = null,
    decimal? AmountMaxUsd = null,
    int? DurationYears = null,
    bool? Renewable = null,
    bool? VnEligible = null,
    string? ApplicationMethod = null,
    string? ApplyUrl = null);

public interface IScholarshipSearchRepository
{
    Task<PagedResult<ScholarshipDto>> SearchAsync(ScholarshipSearchQuery query, CancellationToken ct = default);
}

public interface IScholarshipSearchService
{
    Task<PagedResult<ScholarshipDto>> SearchAsync(ScholarshipSearchQuery query, CancellationToken ct = default);
}

public class ScholarshipSearchService(IScholarshipSearchRepository repository) : IScholarshipSearchService
{
    public async Task<PagedResult<ScholarshipDto>> SearchAsync(ScholarshipSearchQuery query, CancellationToken ct = default)
    {
        var page = Math.Max(query.Page, 1);
        var size = Math.Clamp(query.PageSize, 1, 100);
        var normalized = query with { Page = page, PageSize = size };
        if (query.MinAmountUsd > query.MaxAmountUsd)
            normalized = normalized with { MinAmountUsd = query.MaxAmountUsd, MaxAmountUsd = query.MinAmountUsd };

        var result = await repository.SearchAsync(normalized, ct);
        var items = result.Items
            .Select(s => s with
            {
                SourceUrl = UniversityDetailService.PublicUrl(s.SourceUrl),
                ApplyUrl = UniversityDetailService.PublicUrl(s.ApplyUrl),
            })
            .ToList();
        return result with { Items = items };
    }
}
