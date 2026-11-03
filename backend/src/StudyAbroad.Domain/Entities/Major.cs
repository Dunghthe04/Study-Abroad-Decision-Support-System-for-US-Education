using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Danh mục ngành chuẩn theo nhóm CIP 2020 (CDS mục J1).</summary>
public class Major : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    /// <summary>mã CIP 2020, ví dụ 11 (nhóm) hoặc 11.0701 (ngành cụ thể)</summary>
    public string? CipCode { get; set; }
}
