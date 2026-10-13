using StudyAbroad.Application.Profile.Flow;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// [USAS-640] DTO yêu cầu gửi ảnh học bạ sang advisor để nhận diện OCR
/// </summary>
public record TranscriptOcrRequest(
    string ImageBase64,
    string? FileName = null,
    string? ContentType = null
);

/// <summary>
/// [USAS-640] DTO kết quả nhận diện OCR bảng điểm trả về từ advisor
/// </summary>
public record TranscriptOcrResponse(
    bool Success,
    string? ModelUsed,
    IReadOnlyList<FlowTranscriptTermDto> Terms,
    decimal? CalculatedGpa4,
    string? Message
);

/// <summary>
/// [USAS-640] Client giao tiếp với Python FastAPI Advisor để OCR bảng điểm
/// </summary>
public interface ITranscriptOcrClient
{
    Task<TranscriptOcrResponse> OcrTranscriptAsync(TranscriptOcrRequest request, CancellationToken ct = default);
}
