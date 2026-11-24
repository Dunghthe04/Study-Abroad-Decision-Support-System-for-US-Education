namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Trả lời của advisor khi nhờ tạo vector cho kho tri thức. Pending: số dòng còn thiếu vector; Started: false nếu không có gì để làm
/// hoặc đang có một lần chạy khác; Done, Error: của lần chạy gần nhất.
/// </summary>
public record KnowledgeEmbedDto(int Pending, bool Running, int Done, string? Error, bool Started);
