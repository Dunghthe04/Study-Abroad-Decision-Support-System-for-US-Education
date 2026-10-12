using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Service cung cấp siêu dữ liệu động cho luồng hồ sơ (tuyệt đối không fix cứng code).
/// </summary>
public interface IProfileMetadataService
{
    Task<ProfileMetadataResponse> GetMetadataAsync(CancellationToken ct = default);
    void InvalidateCache();
}
