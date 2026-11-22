using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>
/// [UC-85] Một lần phân tích độ nhạy tham số của thuật toán gợi ý trường. Chỉ được lưu khi phân tích chạy xong trọn vẹn
/// (lỗi giữa chừng thì không có dòng nào). Ghi đủ điều kiện đo (phiên bản thuật toán, bộ hồ sơ, dữ liệu trường) để biết
/// hai lần chạy có so được với nhau không. Không chứa dữ liệu học sinh thật: chỉ chạy trên bộ hồ sơ giả lập.
/// </summary>
public class SensitivityRun : BaseEntity
{
    /// <summary>Admin chạy phân tích; null nếu tài khoản đã bị xóa.</summary>
    public Guid? CreatedByUserId { get; set; }
    /// <summary>Phiên bản thuật toán lúc chạy (RecommendationService.AlgorithmVersion).</summary>
    public string AlgorithmVersion { get; set; } = string.Empty;

    /// <summary>Bộ hồ sơ giả lập: phiên bản, seed, số hồ sơ và hash của file.</summary>
    public string ProfileSetVersion { get; set; } = string.Empty;
    public int ProfileSetSeed { get; set; }
    public int ProfileCount { get; set; }
    public string ProfileSetHash { get; set; } = string.Empty;

    /// <summary>Số trường và dấu nhận biết dữ liệu trường lúc chạy.</summary>
    public int SchoolCount { get; set; }
    public string SchoolDataHash { get; set; } = string.Empty;

    /// <summary>Yêu cầu của admin (JSON): các tham số đã chọn, mức nhích, hai ngưỡng nhãn.</summary>
    public string RequestJson { get; set; } = "{}";
    /// <summary>Giá trị của MỌI tham số trong bộ đang chạy lúc phân tích (JSON), để biết các con số được đo quanh mốc nào.</summary>
    public string ParametersJson { get; set; } = "{}";
    /// <summary>Kết quả từng tham số (JSON): giá trị gốc, từng mức nhích, tỉ lệ thay đổi, nhãn.</summary>
    public string ResultJson { get; set; } = "[]";

    /// <summary>Số tham số theo nhãn, để danh sách lần chạy hiện tóm tắt mà không phải đọc ResultJson.</summary>
    public int StableCount { get; set; }
    public int WatchCount { get; set; }
    public int SensitiveCount { get; set; }

    /// <summary>Thời gian chạy (mili giây).</summary>
    public int DurationMs { get; set; }
}
