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
    public decimal? OtherExpensesUsd { get; set; }
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

    // ---- Số liệu cho công thức gợi ý trường (GOI_Y_TRUONG_TONG_QUAN). Tỉ lệ đều ở dạng 0–1. ----

    /// <summary>Tỉ lệ nhận du học sinh (CDS C1). Dùng tính r = tỉ lệ du học sinh / tỉ lệ chung để hạ nhóm.</summary>
    public decimal? IntlAcceptanceRate { get; set; }
    /// <summary>Mức coi trọng GPA (CDS C7): very_important | important | considered | not_considered</summary>
    public string? C7GpaImportance { get; set; }
    /// <summary>Mức coi trọng điểm thi chuẩn hóa (CDS C7), cùng thang với C7GpaImportance</summary>
    public string? C7TestImportance { get; set; }
    /// <summary>Mức coi trọng ngoại khóa (CDS C7), cùng thang với C7GpaImportance</summary>
    public string? C7EcImportance { get; set; }
    /// <summary>Tỉ lệ sinh viên trúng tuyển có nộp SAT (CDS C9). Dưới 0,5 thì L của SAT × 0,5.</summary>
    public decimal? SatSubmitPct { get; set; }
    /// <summary>Phân phối GPA sinh viên trúng tuyển (CDS C11): tỉ lệ trong từng khoảng.</summary>
    public decimal? GpaPct40 { get; set; }
    public decimal? GpaPct375To399 { get; set; }
    public decimal? GpaPct350To374 { get; set; }
    public decimal? GpaPct325To349 { get; set; }
    public decimal? GpaPct300To324 { get; set; }
    public decimal? GpaPct250To299 { get; set; }
    public decimal? GpaPctBelow250 { get; set; }
    /// <summary>Học bổng cho du học sinh (CDS H6): need_only | merit_only | need_and_merit | none</summary>
    public string? IntlAidPolicy { get; set; }
    /// <summary>Học bổng trung bình mỗi du học sinh nhận (USD/năm, CDS H6). Dùng tính H ở tầng 3.</summary>
    public decimal? IntlAidAvgUsd { get; set; }
    /// <summary>Tỉ lệ giữ chân năm hai (CDS B22 / College Scorecard RET_FT4). Dùng tính Q ở tầng 3.</summary>
    public decimal? RetentionRate1Yr { get; set; }
    /// <summary>Tỉ lệ tốt nghiệp trong 6 năm (CDS B / College Scorecard C150_4). Dùng tính Q ở tầng 3.</summary>
    public decimal? GradRate6Yr { get; set; }

    public string? Notes { get; set; }
    public string? SourceUrl { get; set; }
    public DateOnly? RetrievedAt { get; set; }
}
