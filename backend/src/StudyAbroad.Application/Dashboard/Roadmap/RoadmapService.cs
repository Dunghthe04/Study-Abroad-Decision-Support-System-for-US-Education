using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Dashboard.Roadmap;

public class RoadmapService(IProfileFlowRepository profileFlowRepository) : IRoadmapService
{
    public async Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default)
    {
        string? canonical = null;
        if (!string.IsNullOrWhiteSpace(province))
        {
            canonical = VietnamProvinces.Resolve(province);
            if (canonical is null)
                return SetProvinceResult.InvalidProvince;
        }

        // không tạo hồ sơ mới chỉ để lưu tỉnh: phụ huynh không có hồ sơ riêng, học sinh chưa có hồ sơ thì chưa có lộ trình
        var profile = await profileFlowRepository.GetProfileByUserIdAsync(studentId, ct);
        if (profile is null)
            return SetProvinceResult.NoProfile;

        profile.Province = canonical;
        await profileFlowRepository.UpdateProfileAsync(profile, ct);
        return SetProvinceResult.Saved;
    }
}
