using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Một cuộc chat với AI tư vấn (backlog #10).</summary>
public class ChatSession : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Title { get; set; }
    public string? StudyLevel { get; set; }
    /// <summary>bước trong flow: B0..B6</summary>
    public string? CurrentStep { get; set; }
    public Guid? SkillId { get; set; }
}
