using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Điểm từng môn theo từng kỳ trong bảng điểm (backlog #2, #4).</summary>
public class TranscriptScore : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>ví dụ Lớp 10 HK1 (= "{GradeName} HK{Semester}" với dữ liệu luồng 6 bước)</summary>
    public string TermName { get; set; } = string.Empty;
    public int TermOrder { get; set; }
    /// <summary>[USAS-640] khối/năm học, ví dụ Lớp 10, Năm 1; null với dữ liệu cũ</summary>
    public string? GradeName { get; set; }
    /// <summary>[USAS-640] học kỳ 1 | 2; null với dữ liệu cũ</summary>
    public int? Semester { get; set; }
    public string Subject { get; set; } = string.Empty;
    /// <summary>Điểm số đã chuẩn hóa (thang 10 / thang 4)</summary>
    public decimal Score { get; set; }
    /// <summary>Điểm nhập thô gốc (ví dụ 'A+', '8.5', '95')</summary>
    public string? RawScore { get; set; }
    public decimal? Credits { get; set; }
}
