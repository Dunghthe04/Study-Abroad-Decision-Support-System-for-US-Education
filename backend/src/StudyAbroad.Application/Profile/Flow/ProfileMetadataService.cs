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

        // Nạp và hợp nhất toàn bộ 366+ ngành học thực tế từ các trường trong CSDL (đảm bảo không bao giờ thiếu ngành)
        var offeringMajors = await repository.GetOfferingMajorsAsync(ct);
        if (offeringMajors.Count > 0)
        {
            var existingEnglishNames = new HashSet<string>(metadata.UsMajors.Select(m => m.EnglishName.Trim()), StringComparer.OrdinalIgnoreCase);
            var mergedMajors = new List<UsMajorMetadataItem>(metadata.UsMajors);

            foreach (var major in offeringMajors)
            {
                if (!existingEnglishNames.Contains(major))
                {
                    var id = Slugify(major);
                    mergedMajors.Add(new UsMajorMetadataItem(id, major, major, "Chuyên ngành đại học"));
                    existingEnglishNames.Add(major);
                }
            }

            metadata = metadata with
            {
                UsMajors = mergedMajors.OrderBy(m => m.EnglishName).ToList()
            };
        }

        cache.Set(CacheKey, metadata, CacheDuration);
        return metadata;
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
