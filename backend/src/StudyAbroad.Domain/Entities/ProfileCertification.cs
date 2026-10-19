using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>[USAS-640] Một chứng chỉ / bài thi học sinh khai ở bước 3. Mỗi môn AP là một dòng.</summary>
public class ProfileCertification : BaseEntity
{
    public Guid StudentProfileId { get; set; }
    /// <summary>english | standardized | graduate (theo category trong metadata certifications)</summary>
    public string Category { get; set; } = string.Empty;
    /// <summary>id loại trong metadata certifications (ielts, toefl, sat, ap...) hoặc "other"</summary>
    public string TestType { get; set; } = string.Empty;
    /// <summary>tên tự nhập khi TestType = other</summary>
    public string? CustomName { get; set; }
    /// <summary>tên môn với AP / A-Level</summary>
    public string? Subject { get; set; }
    public decimal? OverallScore { get; set; }
    /// <summary>thang điểm tự nhập khi TestType = other ("0–100"), hoặc điểm dạng chữ (A-Level "A*")</summary>
    public string? ScaleText { get; set; }
    /// <summary>điểm thành phần dạng {"listening": 7, "reading": 6.5}; khóa theo sections trong metadata</summary>
    public string? SectionScoresJson { get; set; }
    /// <summary>ngày 1 của tháng thi; hạn dùng tính từ đây + validityYears trong metadata, không lưu</summary>
    public DateOnly? TestDate { get; set; }
    public int SortOrder { get; set; }
}
