using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Profile.Academic;

/// <summary>Đọc bảng quy đổi grade_scale.10 đang dùng (từ DB, có cache).</summary>
public interface IGradeScaleProvider
{
    /// <summary>Bảng trong app_settings nếu hợp lệ, không thì bảng mặc định.</summary>
    Task<GradeScaleConfigDto> GetAsync(CancellationToken ct = default);

    /// <summary>Xoá cache để lần đọc sau lấy bảng mới (gọi sau khi lưu bảng).</summary>
    void Invalidate();
}

/// <summary>
/// Đọc grade_scale.10 từ app_settings, cache 10 phút như ProfileMetadataService.
/// Bảng thiếu hoặc không hợp lệ thì dùng bảng mặc định để không làm hỏng việc tính GPA.
/// </summary>
public class GradeScaleProvider(IAcademicAnalysisRepository repository, IMemoryCache cache) : IGradeScaleProvider
{
    private const string CacheKey = "grade_scale_10_cache";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<GradeScaleConfigDto> GetAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out GradeScaleConfigDto? cached) && cached is not null)
            return cached;

        var setting = await repository.GetAppSettingByKeyAsync(AcademicConstants.GradeScale10AppSettingKey, ct);
        var config = GradeScale.Parse(setting?.ValueJson) ?? AcademicAnalysisService.GetDefaultScaleConfig();

        cache.Set(CacheKey, config, CacheDuration);
        return config;
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
