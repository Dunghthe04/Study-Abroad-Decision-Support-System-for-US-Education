namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// [USAS-365] DTO đại diện cho một đầu điểm môn học trong bảng điểm học sinh.
/// </summary>
public record TranscriptScoreDto(
    Guid Id,
    string TermName,
    int TermOrder,
    string Subject,
    decimal Score,
    decimal? Credits,
    decimal Gpa4,
    string SubjectGroup,
    string SubjectGroupName
);

/// <summary>
/// [USAS-365] DTO gửi lên để thêm hoặc sửa một đầu điểm môn học.
/// </summary>
public record UpsertTranscriptScoreItem(
    Guid? Id,
    string TermName,
    int TermOrder,
    string Subject,
    decimal Score,
    decimal? Credits
);

/// <summary>
/// [USAS-365] Request gửi lên danh sách bảng điểm để lưu hàng loạt (Batch Upsert).
/// </summary>
public record BatchTranscriptScoresRequest(
    List<UpsertTranscriptScoreItem> Scores
);

/// <summary>
/// [USAS-365] DTO thống kê điểm theo từng nhóm môn học (STEM, Xã hội, Ngoại ngữ).
/// </summary>
public record SubjectGroupScoreDto(
    string GroupKey,
    string GroupName,
    decimal RawAverage,
    decimal Gpa4,
    int SubjectsCount,
    List<string> Subjects
);

/// <summary>
/// [USAS-365] DTO thống kê điểm và xu hướng theo từng học kỳ.
/// </summary>
public record TermTrendDto(
    int TermOrder,
    string TermName,
    decimal RawAverage,
    decimal Gpa4,
    int SubjectCount
);

/// <summary>
/// [USAS-365] Quy tắc quy đổi điểm chi tiết theo thang.
/// </summary>
public record GradeScaleRuleDto(
    decimal MinScore,
    decimal MaxScore,
    decimal Gpa4,
    string LetterGrade
);

/// <summary>
/// [USAS-365] Cấu hình thang quy đổi điểm đọc từ app_settings hoặc fallback WES.
/// </summary>
public record GradeScaleConfigDto(
    string ScaleKey,
    string SourceName,
    string Method,
    List<GradeScaleRuleDto> Rules
);

/// <summary>
/// [USAS-365] Kết quả phân tích học thuật toàn diện trả về cho học sinh và AI gợi ý trường.
/// </summary>
public record AcademicAnalysisResponseDto(
    Guid AnalysisId,
    Guid StudentProfileId,
    decimal UnweightedGpa,
    decimal WeightedGpa,
    decimal RawAverage,
    int TotalSubjects,
    int TotalTerms,
    string Trend,
    string TrendDescription,
    string ScaleSource,
    List<SubjectGroupScoreDto> SubjectGroups,
    List<TermTrendDto> TermAverages,
    string Disclaimer,
    DateTime CreatedAt
);
