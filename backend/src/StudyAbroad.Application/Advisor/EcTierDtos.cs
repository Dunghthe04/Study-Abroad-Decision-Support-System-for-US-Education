using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor;

/// <summary>Một hoạt động gửi advisor: tên (kèm mô tả), phạm vi học sinh chọn 1–5, số tháng, mã vai trò</summary>
public record EcActivityDto(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("scope")] int Scope,
    [property: JsonPropertyName("months")] int Months,
    [property: JsonPropertyName("role")] string? Role);

/// <summary>Một giải thưởng gửi advisor: tên và cấp giải 1–5</summary>
public record EcAwardDto(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("level")] int Level);

/// <summary>Dữ liệu gửi POST api/v1/recommend/ec (khớp EcRequest bên Python, mỗi mảng tối đa 10)</summary>
public record EcTierRequest(
    [property: JsonPropertyName("act")] IReadOnlyList<EcActivityDto> Act,
    [property: JsonPropertyName("awards")] IReadOnlyList<EcAwardDto> Awards);

/// <summary>Phần cần dùng trong EcResponse bên Python: bậc 1–5 và SLM có đọc được hay đã dùng vai trò học sinh chọn</summary>
public record EcTierResponse(
    [property: JsonPropertyName("tier")] int Tier,
    [property: JsonPropertyName("ai_used")] bool AiUsed);

/// <summary>Gọi advisor: SLM xác nhận vai trò, phạm vi từng hoạt động rồi Python tính bậc ngoại khóa</summary>
public interface IEcTierClient
{
    /// <summary>Bậc 1–5; null khi advisor tắt, lỗi hoặc trả sai để bên gọi tự tính dự phòng</summary>
    Task<int?> ReadTierAsync(EcTierRequest request, CancellationToken ct = default);
}
