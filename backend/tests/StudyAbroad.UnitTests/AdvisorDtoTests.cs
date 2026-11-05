using System.Text.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

/// <summary>Hợp đồng chat với advisor (Python): JSON hai chiều, cũ và mới đều đọc được.</summary>
public class AdvisorDtoTests
{
    // AdvisorClient gọi PostAsJsonAsync / ReadFromJsonAsync với cấu hình mặc định này (camelCase, không phân biệt hoa thường)
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Response_WithNewFieldsFromAdvisor_IsDeserialised()
    {
        const string json = """
            {
              "answer": "Bạn chọn ngành nhé",
              "citations": [],
              "disclaimer": null,
              "state": {"currentStep": "B4"},
              "skills": ["skill_lo_trinh"],
              "quickReplies": [{"label": "Điền bằng form", "value": "Điền bằng form"}],
              "widget": {"type": "major", "data": {"options": ["Computer Science"]}},
              "profile": {"gpa": {"value": 3.4, "status": "confirmed"}},
              "profileChanged": ["gpa"]
            }
            """;

        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(json, Web)!;

        var button = Assert.Single(res.QuickReplies!);
        Assert.Equal("Điền bằng form", button.Label);
        Assert.Equal("Điền bằng form", button.Value);
        Assert.Equal("major", res.Widget!.Type);
        Assert.Equal("Computer Science", res.Widget.Data!.Value.GetProperty("options")[0].GetString());
        Assert.Equal("confirmed", res.Profile!.Value.GetProperty("gpa").GetProperty("status").GetString());
        Assert.Equal(["gpa"], res.ProfileChanged);
    }

    [Fact]
    public void Response_WithEmptyNewFields_IsDeserialisedAsEmpty()
    {
        // Đúng những gì advisor trả khi chưa có nút, widget, hồ sơ
        const string json = """
            {"answer": "ok", "citations": [], "disclaimer": null, "state": {"currentStep": "B0"},
             "skills": [], "quickReplies": [], "widget": null, "profile": {}, "profileChanged": []}
            """;

        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(json, Web)!;

        Assert.Empty(res.QuickReplies!);
        Assert.Null(res.Widget);
        Assert.Empty(res.ProfileChanged!);
    }

    [Fact]
    public void Response_FromOldAdvisorWithoutNewFields_StillLoads()
    {
        const string json = """{"answer": "ok", "citations": [], "disclaimer": null, "state": {"currentStep": "B0"}}""";

        var res = JsonSerializer.Deserialize<AdvisorChatResponse>(json, Web)!;

        Assert.Equal("ok", res.Answer);
        Assert.Null(res.QuickReplies);
        Assert.Null(res.Widget);
        Assert.Null(res.ProfileChanged);
    }

    [Fact]
    public void Request_SendsProfileInCamelCase_AndNullWhenNotGiven()
    {
        var hello = new List<ChatMessage> { new("user", "chào") };
        var profile = JsonDocument.Parse("""{"gpa": {"value": 3.4, "status": "confirmed"}}""").RootElement;

        var withProfile = JsonSerializer.Serialize(new AdvisorChatRequest(hello, Profile: profile), Web);
        var without = JsonSerializer.Serialize(new AdvisorChatRequest(hello), Web);

        using var sent = JsonDocument.Parse(withProfile);
        Assert.Equal("confirmed", sent.RootElement.GetProperty("profile").GetProperty("gpa").GetProperty("status").GetString());
        using var plain = JsonDocument.Parse(without);
        Assert.Equal(JsonValueKind.Null, plain.RootElement.GetProperty("profile").ValueKind); // Python nhận null = không gửi
    }
}
