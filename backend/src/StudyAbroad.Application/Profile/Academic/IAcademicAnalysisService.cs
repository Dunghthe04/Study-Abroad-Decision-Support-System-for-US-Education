namespace StudyAbroad.Application.Profile.Academic;

/// <summary>
/// [USAS-365] Interface Service phụ trách nghiệp vụ tính toán GPA, quy đổi thang 4.0, phân nhóm môn học và đánh giá xu hướng.
/// </summary>
public interface IAcademicAnalysisService
{
    /// <summary>
    /// [USAS-365] Lấy danh sách bảng điểm chi tiết của học sinh đang đăng nhập.
    /// </summary>
    Task<IReadOnlyList<TranscriptScoreDto>> GetScoresAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lưu danh sách đầu điểm vào bảng điểm, kiểm tra tính hợp lệ và quyền sở hữu Anti-IDOR.
    /// </summary>
    Task<IReadOnlyList<TranscriptScoreDto>> SaveScoresAsync(Guid userId, BatchTranscriptScoresRequest request, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Xóa một đầu điểm trong bảng điểm của học sinh.
    /// </summary>
    Task DeleteScoreAsync(Guid userId, Guid scoreId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Thực hiện thuật toán phân tích học thuật: quy đổi GPA 4.0, tính Weighted GPA, phân nhóm STEM/Xã hội/Ngoại ngữ, đánh giá xu hướng 3 năm.
    /// </summary>
    Task<AcademicAnalysisResponseDto> AnalyzeAcademicProfileAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lấy kết quả phân tích học thuật gần nhất đã lưu trong cơ sở dữ liệu.
    /// </summary>
    Task<AcademicAnalysisResponseDto?> GetLatestAnalysisAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// [USAS-365] Lấy cấu hình bảng quy đổi thang điểm hiện hành (đọc từ app_settings hoặc fallback WES).
    /// </summary>
    Task<GradeScaleConfigDto> GetGradeScaleConfigAsync(CancellationToken ct = default);
}
