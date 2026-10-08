using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Hồ sơ học sinh: học thuật, điểm thi, tài chính và tiến độ lộ trình (backlog #2, #3, #18, #21). Mỗi user một hồ sơ.</summary>
public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>bậc muốn đi, giá trị từ StudyLevels</summary>
    public string TargetLevel { get; set; } = string.Empty;
    public string? CurrentSchool { get; set; }
    /// <summary>ví dụ Lớp 11, Năm 3, Đã tốt nghiệp</summary>
    public string? CurrentGrade { get; set; }
    /// <summary>thang điểm của bảng điểm gốc (transcript_scores): 10 | 4 | 100. Không phải thang của OverallGpa</summary>
    public string GradeScale { get; set; } = "10";
    /// <summary>GPA luôn ở thang 4, do phân tích học thuật quy đổi từ bảng điểm; form không cho nhập tay; null nếu chưa phân tích</summary>
    public decimal? OverallGpa { get; set; }
    public string? IntendedMajor { get; set; }
    public decimal? Ielts { get; set; }
    public decimal? Toefl { get; set; }
    public decimal? Duolingo { get; set; }
    public decimal? Sat { get; set; }
    public decimal? Act { get; set; }
    public decimal? Gre { get; set; }
    public decimal? Gmat { get; set; }
    /// <summary>điểm thành phần, kỳ thi khác, ngày thi</summary>
    public string? OtherTestsJson { get; set; }
    /// <summary>điểm ngoại khóa 0–4 (thang của thầy): LLM đọc hoạt động thành thuộc tính, công thức của nhóm tính điểm; null nếu chưa phân tích</summary>
    public decimal? ExtracurricularScore { get; set; }
    public decimal? AnnualBudgetUsd { get; set; }
    /// <summary>family | loan | scholarship | other</summary>
    public string? FundingSource { get; set; }
    public bool NeedsScholarship { get; set; }
    public List<string> PreferredStates { get; set; } = [];
    /// <summary>tiến độ lộ trình theo StepKey, ví dụ B1: done</summary>
    public string? RoadmapProgressJson { get; set; }
}
