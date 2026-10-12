using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Domain.Constants;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service cung cấp siêu dữ liệu động cho luồng hồ sơ (tuyệt đối không fix cứng code).
/// Tích hợp In-Memory Cache 10 phút để giảm tải truy vấn DB.
/// </summary>
public class ProfileMetadataService(
    IProfileFlowRepository repository,
    IMemoryCache cache) : IProfileMetadataService
{
    private const string CacheKey = "profile_flow_metadata_cache";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<ProfileMetadataResponse> GetMetadataAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out ProfileMetadataResponse? cached) && cached is not null)
        {
            return cached;
        }

        var json = await repository.GetAppSettingValueAsync(ProfileConstants.ProfileMetadataAppSettingKey, ct);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException(
                $"Không tìm thấy cấu hình siêu dữ liệu '{ProfileConstants.ProfileMetadataAppSettingKey}' trong bảng app_settings.");
        }

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var metadata = JsonSerializer.Deserialize<ProfileMetadataResponse>(json, options)
            ?? throw new InvalidOperationException("Không thể phân tích dữ liệu cấu hình metadata hồ sơ.");

        cache.Set(CacheKey, metadata, CacheDuration);
        return metadata;
    }

    public void InvalidateCache()
    {
        cache.Remove(CacheKey);
    }
}
