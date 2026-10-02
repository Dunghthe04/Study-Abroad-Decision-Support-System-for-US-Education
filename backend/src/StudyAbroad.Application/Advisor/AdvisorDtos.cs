namespace StudyAbroad.Application.Advisor;

public record ChatMessage(string Role, string Content);

/// <param name="StudyLevel">Level the user asks about (see Domain.Constants.StudyLevels); null = all levels.</param>
public record AdvisorChatRequest(IReadOnlyList<ChatMessage> Messages, string? SessionId = null, string? StudyLevel = null);

public record Citation(string Title, string? Url, string? Snippet);

public record AdvisorChatResponse(string Answer, IReadOnlyList<Citation> Citations, string? Disclaimer);
