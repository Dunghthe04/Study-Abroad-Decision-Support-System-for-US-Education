using System.Text.Json;
using System.Text.Json.Nodes;

namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>
/// Bản rút gọn lộ trình gửi advisor mỗi lượt chat (bước C1): giữ nguyên số liệu của Tổng quan, bỏ danh sách trung tâm
/// của từng nhiệm vụ (chat không gợi ý trung tâm ở đây, chỉ đưa link Tổng quan). Khóa camelCase như mọi dữ liệu gửi advisor.
/// </summary>
public static class RoadmapForAdvisor
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static JsonElement? Slim(RoadmapDto? roadmap)
    {
        if (roadmap is null)
            return null;
        var node = JsonSerializer.SerializeToNode(roadmap, Web)!.AsObject();
        foreach (var task in node["tasks"]!.AsArray())
            task!.AsObject().Remove("centers");
        return JsonSerializer.SerializeToElement(node, Web);
    }
}
