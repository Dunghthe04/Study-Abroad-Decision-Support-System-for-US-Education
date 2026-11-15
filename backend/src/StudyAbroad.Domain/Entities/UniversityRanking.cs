namespace StudyAbroad.Domain.Entities;

/// <summary>Hạng của một trường trong một bảng xếp hạng.</summary>
public class UniversityRanking
{
    public Guid UniversityId { get; set; }
    public string ListId { get; set; } = string.Empty;
    public int Rank { get; set; }
    public bool IsTie { get; set; }
    /// <summary>hạng để hiển thị, ví dụ #34 (tie)</summary>
    public string RankDisplay { get; set; } = string.Empty;
}
