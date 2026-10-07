using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Điểm từng môn theo từng kỳ trong bảng điểm (backlog #2, #4).</summary>
public class TranscriptScore : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>ví dụ Lớp 10 HK1</summary>
    public string TermName { get; set; } = string.Empty;
    public int TermOrder { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>Điểm số đã chuẩn hóa (thang 10 / thang 4)</summary>
    public decimal Score { get; set; }
    /// <summary>Điểm nhập thô gốc (ví dụ 'A+', '8.5', '95')</summary>
    public string? RawScore { get; set; }
    public decimal? Credits { get; set; }
}
