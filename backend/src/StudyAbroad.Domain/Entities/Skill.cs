using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Flow tư vấn (skill) cho AI, sinh từ sheet 07_Flow. Mỗi phiên bản một dòng, chỉ một phiên bản active (backlog #10).</summary>
public class Skill : BaseEntity
{
    /// <summary>ví dụ visa_f1</summary>
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>các bước, câu hỏi, luật</summary>
    public string DefinitionJson { get; set; } = "{}";
    public bool IsActive { get; set; }
}
