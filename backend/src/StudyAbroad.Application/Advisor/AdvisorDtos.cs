using System.Text.Json;
namespace StudyAbroad.Application.Advisor;

public record ChatMessage(string Role, string Content);

/// <param name="StudyLevel">Level the user asks about (see Domain.Constants.StudyLevels); null = all levels.</param>
public record AdvisorChatRequest(
    IReadOnlyList<ChatMessage> Messages,
    string? SessionId = null,
    string? StudyLevel = null,
    JsonElement? State = null);
public record Citation(string Title, string? Url, string? Snippet);

public record AdvisorChatResponse(
    string Answer,
    IReadOnlyList<Citation> Citations,
    string? Disclaimer,
    JsonElement? State = null,
    string? SessionId = null);