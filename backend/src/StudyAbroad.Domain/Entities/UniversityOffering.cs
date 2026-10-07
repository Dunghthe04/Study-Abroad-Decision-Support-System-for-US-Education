using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Thông tin của một trường cho một bậc học: ngành, chi phí, điều kiện, hạn nộp (backlog #5, #14, #18).</summary>
public class UniversityOffering : BaseEntity
{
    public Guid UniversityId { get; set; }
    public string StudyLevel { get; set; } = string.Empty;
    public List<string> Majors { get; set; } = [];
    public List<string> StemMajors { get; set; } = [];
    /// <summary>năm học của số liệu chi phí, ví dụ 2026-27</summary>
    public string? AcademicYear { get; set; }
    public decimal? TuitionUsd { get; set; }
    public decimal? LivingUsd { get; set; }
    public decimal? FeesUsd { get; set; }
    public decimal? MinGpa4 { get; set; }
    public decimal? MinIelts { get; set; }
    public decimal? MinToefl { get; set; }
    public decimal? MinDuolingo { get; set; }
    /// <summary>GPA trung bình của sinh viên trúng tuyển, thang 4 (Common Data Set mục C12). Null nếu trường không công bố.</summary>
    public decimal? AvgGpa4 { get; set; }
    /// <summary>Tổng SAT mốc 25% của sinh viên trúng tuyển (400–1600). Dưới mốc này → Reach.</summary>
    public int? Sat25 { get; set; }
    /// <summary>Tổng SAT mốc 75% của sinh viên trúng tuyển (400–1600). Từ mốc này trở lên → Safety.</summary>
    public int? Sat75 { get; set; }
    /// <summary>required | optional | not_accepted</summary>
    public string? SatPolicy { get; set; }
    /// <summary>danh sách vòng nộp và ngày: ED, EA, RD, Spring...</summary>
    public string? DeadlinesJson { get; set; }
    public string? Notes { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? RetrievedAt { get; set; }
}
