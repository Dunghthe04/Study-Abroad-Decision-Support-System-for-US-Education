using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

public class Media : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;// Univeristy | Center | M
    public Guid EntityId { get; set; }
    public string Type { get; set; } = string.Empty; // Logo / Banner /
    public string Url { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    /// <summary>Giấy phép ảnh, vd "CC BY-SA 4.0", "Public domain"; CC BY/BY-SA bắt buộc ghi công khi hiển thị</summary>
    public string? License { get; set; }
    /// <summary>Tác giả ảnh để ghi công</summary>
    public string? Author { get; set; }
    /// <summary>Trang gốc của ảnh (vd trang file trên Wikimedia Commons)</summary>
    public string? SourcePageUrl { get; set; }
}
