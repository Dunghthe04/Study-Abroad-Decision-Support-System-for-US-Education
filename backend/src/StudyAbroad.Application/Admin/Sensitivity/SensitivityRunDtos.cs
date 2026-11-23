using StudyAbroad.Application.Recommendations.Sensitivity;

namespace StudyAbroad.Application.Admin.Sensitivity;

// [UC-85] Một dòng trong danh sách lần chạy (không kèm kết quả chi tiết).
public sealed record SensitivityRunListRow(
    Guid Id, DateTime CreatedAt, Guid? CreatedByUserId, string? CreatedByName,
    string AlgorithmVersion, string ProfileSetVersion, string ProfileSetHash, int ProfileCount,
    int SchoolCount, string SchoolDataHash,
    IReadOnlyList<string> ParameterKeys, IReadOnlyList<double> Steps,
    int StableCount, int WatchCount, int SensitiveCount, int DurationMs);

// [UC-85] Chi tiết một lần chạy: thông tin chung, yêu cầu, giá trị mọi tham số lúc chạy và kết quả đầy đủ.
public sealed record SensitivityRunDetail(
    SensitivityRunListRow Summary,
    SensitivityRequest Request,
    IReadOnlyDictionary<string, double> Parameters,
    SensitivityReport Report);
