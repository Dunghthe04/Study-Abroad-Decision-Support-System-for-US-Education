namespace StudyAbroad.Domain.Entities;

/// <summary>Bằng chứng xác minh nguồn dữ liệu (Data Provenance) cho từng thuộc tính trường (FE-16: UC-84).</summary>
public class FieldEvidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UniversityId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? FieldValue { get; set; }
    public string? SourceUrl { get; set; }
    public string? SourceSection { get; set; }
    public string? ExtractedQuote { get; set; }
    public string VerifiedBy { get; set; } = "USAS Audit Panel";
    public string Status { get; set; } = "verified";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
