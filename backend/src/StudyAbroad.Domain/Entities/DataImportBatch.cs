namespace StudyAbroad.Domain.Entities;

/// <summary>Lịch sử các đợt nạp dữ liệu ETL và đối chiếu chéo (FE-16: UC-65, UC-84).</summary>
public class DataImportBatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchCode { get; set; } = string.Empty;
    public string? SourceName { get; set; }
    public string? DataYear { get; set; }
    public int TotalRows { get; set; } = 0;
    public int PassedRows { get; set; } = 0;
    public int FlaggedRows { get; set; } = 0;
    public string CheckedBy { get; set; } = "USAS Research Team";
    public string Status { get; set; } = "verified";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
