using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Hồ sơ học sinh: học thuật, điểm thi, tài chính và tiến độ lộ trình (backlog #2, #3, #18, #21). Mỗi user một hồ sơ.</summary>
public class StudentProfile : BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>bậc muốn đi, giá trị từ StudyLevels</summary>
    public string TargetLevel { get; set; } = string.Empty;
    public string? CurrentSchool { get; set; }
    /// <summary>hệ chương trình: standard | specialized | dual_degree | international | private | other</summary>
    public string? EducationSystem { get; set; }
    /// <summary>[USAS-640] cấp học hiện tại (mã metadata currentEducationLevels): middle_school | high_school | college_university | master</summary>
    public string? CurrentEducationLevel { get; set; }
    /// <summary>năm tốt nghiệp THPT hoặc Đại học</summary>
    public int? GraduationYear { get; set; }
    /// <summary>ví dụ Lớp 11, Năm 3, Đã tốt nghiệp</summary>
    public string? CurrentGrade { get; set; }
    /// <summary>thang điểm của bảng điểm gốc (transcript_scores): 10 | 100 | 4 | letter</summary>
    public string GradeScale { get; set; } = "10";
    /// <summary>GPA luôn ở thang 4 (nhóm đã chốt), quy đổi từ bảng điểm; form không cho nhập tay; null nếu chưa phân tích</summary>
    public decimal? OverallGpa { get; set; }
    public string? IntendedMajor { get; set; }
    public decimal? Ielts { get; set; }
    public decimal? Toefl { get; set; }
    public decimal? Duolingo { get; set; }
    public decimal? Sat { get; set; }
    public decimal? Act { get; set; }
    public decimal? Gre { get; set; }
    public decimal? Gmat { get; set; }
    /// <summary>[USAS-640] chứng chỉ tiếng Anh học sinh chọn ở bước 3: ielts | toefl | duolingo | none</summary>
    public string? EnglishTestType { get; set; }
    /// <summary>[USAS-640] ngày thi chứng chỉ tiếng Anh</summary>
    public DateOnly? EnglishTestDate { get; set; }
    /// <summary>kỳ thi khác ngoài cột riêng, dạng {"ap": 4}</summary>
    public string? OtherTestsJson { get; set; }
    /// <summary>điểm ngoại khóa 0–4 (thang của thầy): LLM đọc hoạt động thành thuộc tính, công thức của nhóm tính điểm; null nếu chưa phân tích</summary>
    public decimal? ExtracurricularScore { get; set; }
    /// <summary>[USAS-640] mức ngân sách (mã metadata budgetTiers): tier_under_20k | tier_20k_35k | tier_35k_50k | tier_50k_70k | tier_above_70k</summary>
    public string? BudgetTier { get; set; }
    /// <summary>Khoảng ngân sách tối thiểu (USD/năm)</summary>
    public decimal? BudgetMinUsd { get; set; }
    /// <summary>Khoảng ngân sách tối đa (USD/năm); null khi mức tier_above_70k (không giới hạn trên)</summary>
    public decimal? BudgetMaxUsd { get; set; }
    /// <summary>[USAS-364] Mức học phí mong muốn tối đa hàng năm</summary>
    public decimal? MaxExpectedTuitionUsd { get; set; }
    /// <summary>family | loan | scholarship | other</summary>
    public string? FundingSource { get; set; }
    /// <summary>= ScholarshipNeed là mandatory hoặc preferred; giữ cho code cũ đọc</summary>
    public bool NeedsScholarship { get; set; }
    /// <summary>[USAS-640] nhu cầu học bổng: mandatory | preferred | optional</summary>
    public string? ScholarshipNeed { get; set; }
    /// <summary>[USAS-364] Ghi chú nguyện vọng tài chính của học sinh</summary>
    public string? FinancialNotes { get; set; }
    public List<string> PreferredStates { get; set; } = [];
    /// <summary>Loại hình trường mong muốn: all (Không lọc) | public (Công lập) | private (Tư thục)</summary>
    public string TargetSchoolType { get; set; } = "all";
    /// <summary>tiến độ lộ trình theo StepKey, ví dụ B1: done</summary>
    public string? RoadmapProgressJson { get; set; }
    /// <summary>Đường dẫn tệp bảng điểm đã tải lên (USAS-363)</summary>
    public string? TranscriptFileUrl { get; set; }
    /// <summary>[USAS-640] Trạng thái bản nháp của luồng nhập hồ sơ đa bước (step hiện tại và dữ liệu nháp)</summary>
    public string? DraftFlowStateJson { get; set; }
    /// <summary>[USAS-640] thời điểm học sinh gửi hồ sơ ở bước 6; null nếu chưa gửi lần nào</summary>
    public DateTimeOffset? FlowSubmittedAt { get; set; }
}
