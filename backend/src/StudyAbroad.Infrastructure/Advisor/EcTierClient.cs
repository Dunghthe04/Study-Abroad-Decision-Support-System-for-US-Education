using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Advisor;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Advisor;

/// <summary>
/// Gọi advisor tính bậc ngoại khóa; advisor tắt hoặc lỗi thì trả null, không làm hỏng việc lưu hồ sơ.
/// collector: số đo token của lần gọi được bỏ vào giỏ chung của yêu cầu, ProfileFlowService đổ xuống ai_calls (null thì bỏ qua)
/// </summary>
public class EcTierClient(HttpClient http, ILogger<EcTierClient> logger, IAiUsageCollector? collector = null) : IEcTierClient
{
    public async Task<int?> ReadTierAsync(EcTierRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("api/v1/recommend/ec", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Advisor /ec trả mã {StatusCode}, dùng công thức dự phòng", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<EcTierResponse>(ct);
            // Token đã tiêu thật dù bậc trả về có hợp lệ hay không, nên ghi trước khi kiểm bậc
            collector?.Add(result?.AiUsage);
            if (result is null || result.Tier is < 1 or > 5)
            {
                logger.LogWarning("Advisor /ec trả bậc không hợp lệ, dùng công thức dự phòng");
                return null;
            }

            if (!result.AiUsed)
                logger.LogInformation("SLM không đọc được hoạt động, advisor đã tính bậc theo vai trò học sinh chọn");
            return result.Tier;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Không gọi được advisor /ec, dùng công thức dự phòng");
            return null;
        }
    }
}
