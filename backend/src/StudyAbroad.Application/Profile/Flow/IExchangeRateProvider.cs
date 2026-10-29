using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>Tỷ giá 1 USD = ? VNĐ để form đổi ngân sách VNĐ sang USD (tương đối, nhớ 1 ngày)</summary>
public interface IExchangeRateProvider
{
    Task<decimal> GetVndPerUsdAsync(CancellationToken ct = default);
}
