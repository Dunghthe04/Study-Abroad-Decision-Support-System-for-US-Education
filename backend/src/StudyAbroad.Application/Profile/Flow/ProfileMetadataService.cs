using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Domain.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
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
    IMemoryCache cache,
    IExchangeRateProvider rates) : IProfileMetadataService
{
    private const string CacheKey = "profile_flow_metadata_cache";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public async Task<ProfileMetadataResponse> GetMetadataAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out ProfileMetadataResponse? cached) && cached is not null)
        {
            return cached with { VndPerUsd = await rates.GetVndPerUsdAsync(ct) };
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

        // Ngành chỉ lấy từ bảng majors (ngành có trường đào tạo), không gộp usMajors trong JSON:
        // hồ sơ lưu đúng EnglishName thì HardFilter mới khớp với ngành của trường; Name là tên Việt để hiện
        var majorOptions = await repository.GetMajorOptionsAsync(ct);
        metadata = metadata with
        {
            UsMajors = majorOptions
                .Select(m => new UsMajorMetadataItem(Slugify(m.Name), m.NameVi ?? m.Name, m.Name, null, m.Aliases, m.IsPopular))
                .ToList()
        };

        // Bang chỉ lấy từ bảng us_states (có vùng), JSON không còn usStates / popularStateCodes
        var stateOptions = await repository.GetUsStateOptionsAsync(ct);
        metadata = metadata with
        {
            UsStates = stateOptions
                .Select(s => new UsStateMetadataItem(s.Code, s.Name, s.IsPopular, s.Region))
                .ToList(),
            PopularStateCodes = stateOptions.Where(s => s.IsPopular).Select(s => s.Code).ToList()
        };

        cache.Set(CacheKey, metadata, CacheDuration);
        // Tỷ giá có bộ nhớ riêng (1 ngày) nên gắn sau khi lấy từ cache
        return metadata with { VndPerUsd = await rates.GetVndPerUsdAsync(ct) };
    }

    public void InvalidateCache()
    {
        cache.Remove(CacheKey);
    }

    private static string Slugify(string text)
    {
        return text.ToLowerInvariant()
            .Replace(" ", "_")
            .Replace("-", "_")
            .Replace("/", "_")
            .Replace(",", "")
            .Replace("(", "")
            .Replace(")", "");
    }
}
