using System.Text.Json;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// C4 lộ trình theo trường mục tiêu: chat hỏi tỉnh / thành khi học sinh chưa chọn, advisor trả action "set_province"
/// kèm chữ học sinh gõ (ActionData.province). Ở đây kiểm tra và lưu bằng hàm của trang Tổng quan (IRoadmapService.SetProvinceAsync),
/// rồi viết lại câu trả lời theo kết quả thật. Nút "Xem lộ trình" do advisor gửi, giữ nguyên.
/// </summary>
public static class ChatProvince
{
    public const string Action = "set_province";

    /// <summary>Chữ tỉnh / thành học sinh gõ (ActionData.province), null nếu không có hoặc rỗng.</summary>
    public static string? ProvinceOf(JsonElement? data) =>
        data is { ValueKind: JsonValueKind.Object } d
        && d.TryGetProperty("province", out var p) && p.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(p.GetString())
            ? p.GetString()!.Trim()
            : null;

    public static string SavedAnswer(string province) =>
        $"Mình đã lưu tỉnh / thành của bạn: {province}. Trung tâm gợi ý ở trang Tổng quan đã ưu tiên gần bạn.";

    public static string InvalidAnswer(string typed) =>
        $"Mình chưa nhận ra tỉnh / thành \"{typed}\". Bạn chọn giúp mình ở trang Tổng quan nhé.";

    public const string NoProfileAnswer =
        "Bạn chưa có hồ sơ nên mình chưa lưu được tỉnh / thành. Bạn điền hồ sơ trước rồi chọn tỉnh / thành ở trang Tổng quan nhé.";

    /// <summary>
    /// Lưu tỉnh và thay câu trả lời. Không có chữ tỉnh thì trả nguyên response: SetProvinceAsync nhận rỗng là XÓA tỉnh, nên không bao giờ gọi với rỗng.
    /// </summary>
    public static async Task<AdvisorChatResponse> SaveAsync(
        IRoadmapService roadmap, Guid userId, AdvisorChatResponse response, CancellationToken ct = default)
    {
        var typed = ProvinceOf(response.ActionData);
        if (typed is null)
            return response;
        var answer = await roadmap.SetProvinceAsync(userId, typed, ct) switch
        {
            SetProvinceResult.Saved => SavedAnswer(VietnamProvinces.Resolve(typed) ?? typed),
            SetProvinceResult.InvalidProvince => InvalidAnswer(typed),
            _ => NoProfileAnswer,
        };
        return response with { Answer = answer };
    }
}
