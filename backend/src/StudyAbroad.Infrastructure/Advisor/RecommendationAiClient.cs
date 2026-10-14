using StudyAbroad.Application.Recommendations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Advisor
{
    public class RecommendationAiClient(HttpClient http) : IRecommendationAi
    {
        public Task<AiEcResult> ReadExtracurricularAsync(AiEcRequest request, CancellationToken ct = default) =>
            PostAsync<AiEcRequest, AiEcResult>("recommend/ec", request, ct);

        public Task<AiScoreResult> ScoreAsync(AiScoreRequest request, CancellationToken ct = default) =>
            PostAsync<AiScoreRequest, AiScoreResult>("recommend/score", request, ct);

        public Task<AiPickResult> PickAsync(AiPickRequest request, CancellationToken ct = default) =>
            PostAsync<AiPickRequest, AiPickResult>("recommend/pick", request, ct);

        //Mọi lỗi (mạng, quá thời gian, HTTP lỗi, JSON hỏng) đổi thành RecommendationAiException để service xử lý một chỗ
        private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest request, CancellationToken ct)
        {
            try
            {
                using var response = await http.PostAsJsonAsync(path, request, AiJson.Options, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var detail = await response.Content.ReadAsStringAsync(ct);
                    throw new RecommendationAiException($"Advisor {path} trả {(int)response.StatusCode}: {detail}");
                }
                return await response.Content.ReadFromJsonAsync<TResponse>(AiJson.Options, ct)
                       ?? throw new RecommendationAiException($"Advisor {path} trả nội dung rỗng");
            }
            catch (RecommendationAiException)
            {
                throw;
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                // HttpClient hết thời gian chờ (không phải người dùng hủy)
                throw new RecommendationAiException($"Advisor {path} quá thời gian chờ", ex);
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException or NotSupportedException)
            {
                throw new RecommendationAiException($"Không gọi được advisor {path}: {ex.Message}", ex);
            }
        }
    }
}
