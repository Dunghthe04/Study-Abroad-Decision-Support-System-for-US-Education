using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

public class Media : BaseEntity
{
    public string EntityType { get; set; } = string.Empty;// Univeristy | Center | M
    public Guid EntityId { get; set; }
    public string Type { get; set; } = string.Empty; // Logo / Banner /
    public string Url { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
