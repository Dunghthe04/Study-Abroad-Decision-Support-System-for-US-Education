namespace StudyAbroad.Domain.Entities;

/// <summary>Một bảng xếp hạng, ví dụ U.S. News Best Colleges 2027 – National Universities.</summary>
public class RankingList
{
    /// <summary>mã bảng, ví dụ USN27-NU</summary>
    public string Id { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Tổng quát | Nhóm con | Giá trị | Theo ngành | Khu vực</summary>
    public string? ListGroup { get; set; }
    /// <summary>ordinal | ordinal+range</summary>
    public string? RankType { get; set; }
    public string? Edition { get; set; }
    public int? EditionYear { get; set; }
    public string? Url { get; set; }
}
