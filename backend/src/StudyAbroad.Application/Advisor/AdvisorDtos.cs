using System.Text.Json;
namespace StudyAbroad.Application.Advisor;

public record ChatMessage(string Role, string Content);

/// <param name="StudyLevel">Level the user asks about (see Domain.Constants.StudyLevels); null = all levels.</param>
/// <param name="Profile">Hồ sơ hiện tại (khóa chat → {value, status}) do .NET gửi mỗi lượt; null = không gửi.</param>
public record AdvisorChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? SessionId = null,
    string? StudyLevel = null,
    JsonElement? State = null,
    JsonElement? Profile = null);
public record Citation(string Title, string? Url, string? Snippet);

/// <summary>Nút gợi ý: Label là chữ trên nút, Value là chữ gửi đi như tin nhắn của học sinh khi bấm.</summary>
public record QuickReply(string Label, string Value);

/// <summary>Công cụ nhập ngay trong chat. Type: major | review | budget | scholarship; Data tùy loại.</summary>
public record Widget(string Type, JsonElement? Data = null);

/// <param name="Profile">Hồ sơ sau lượt này (nội bộ, .NET dùng để ghi vào bản nháp).</param>
/// <param name="ProfileChanged">Khóa hồ sơ đổi trong lượt này.</param>
public record AdvisorChatResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    string? Disclaimer,
    JsonElement? State = null,
    string? SessionId = null,
    IReadOnlyList<QuickReply>? QuickReplies = null,
    Widget? Widget = null,
    JsonElement? Profile = null,
    IReadOnlyList<string>? ProfileChanged = null);
