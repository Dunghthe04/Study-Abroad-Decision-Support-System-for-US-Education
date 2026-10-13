using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Advisor;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Advisor;

/// <summary>
/// [USAS-640] Gọi advisor (Python FastAPI) để OCR ảnh học bạ thành danh sách môn học và điểm số.
/// </summary>
public class TranscriptOcrClient(HttpClient http, ILogger<TranscriptOcrClient> logger) : ITranscriptOcrClient
{
    public async Task<TranscriptOcrResponse> OcrTranscriptAsync(TranscriptOcrRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await http.PostAsJsonAsync("/api/v1/profile/transcript-ocr", request, ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<TranscriptOcrResponse>(cancellationToken: ct);
                if (result != null)
                {
                    return result;
                }
            }

            logger.LogWarning("Advisor OCR returned non-success code {StatusCode}", response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to call advisor OCR service, falling back to default highschool template.");
        }

        // Dự phòng khi advisor chưa bật hoặc lỗi mạng: trả về danh sách môn THPT tiêu chuẩn
        return new TranscriptOcrResponse(
            Success: false,
            ModelUsed: "fallback_highschool_template",
            Terms: [],
            CalculatedGpa4: null,
            Message: "Không thể kết nối dịch vụ OCR, bạn có thể nhập điểm thủ công."
        );
    }
}
