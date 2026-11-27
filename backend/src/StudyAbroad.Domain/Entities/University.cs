using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Trường ở Mỹ: đại học, cao đẳng cộng đồng hoặc THPT (backlog #5, #13, #14).</summary>
public class University : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    /// <summary>mã bang, ví dụ CA</summary>
    public string? State { get; set; }
    public string? Website { get; set; }
    /// <summary>public | private</summary>
    public string? Control { get; set; }
    public decimal? AcceptanceRate { get; set; }
    public int? InternationalStudentCount { get; set; }
    public bool IsIvy { get; set; }
    /// <summary>Tên viết tắt học sinh hay gõ (MIT, UT Austin, ASU), dùng khi khớp tên trường trong chat. Một tên có thể nằm ở nhiều trường: khi đó hệ thống hỏi lại.</summary>
    public List<string> Aliases { get; set; } = [];
    public bool IsActive { get; set; } = true;
    /// <summary>Người sửa gần nhất (moderator/admin); ngày sửa là UpdatedAt.</summary>
    public Guid? LastEditedByUserId { get; set; }
}
