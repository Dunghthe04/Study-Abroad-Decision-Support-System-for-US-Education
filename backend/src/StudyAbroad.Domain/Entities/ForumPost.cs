using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Bài viết diễn đàn. Bài gốc (ParentPostId null) là chủ đề và có Title; bài con là trả lời, kể cả do AI (backlog #27, #29).</summary>
public class ForumPost : BaseEntity
{
    public Guid? UserId { get; set; }
    public Guid? ParentPostId { get; set; }
    public string? Title { get; set; }
    public string? StudyLevel { get; set; }
    public string? Category { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public bool IsPinned { get; set; }
    /// <summary>visible | hidden | removed | closed</summary>
    public string Status { get; set; } = "visible";
}
