using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Advisor;

// Khuôn JSON trao đổi với advisor Python: viết lời tư vấn từng trường.
// Nhóm và thứ tự trường do code backend tính (SchoolGrouper, SchoolRanker), không qua advisor.
// Tên viết tắt giữ nguyên key bên Python. Key hoa (L, A, S, F, H, Q, X) phải gắn JsonPropertyName
// vì camelCase sẽ biến chúng thành chữ thường.

// --- Tư vấn từng trường: POST api/v1/recommend/explain ---

// Học sinh. Thiếu số thì null, không gửi 0.
public record ExplainStudentDto(
    double? Gpa,
    int? Sat,
    double? Budget,
    int? Tier,     // bậc ngoại khóa, lấy từ lần gợi ý đã lưu
    string? Major);

// Một trường: số đã lưu + mã lý do cloud đã chọn. Advisor đổi mã why thành câu, Qwen chỉ diễn đạt lại.
public record ExplainSchoolDto(
    string Name,
    string? State,
    string? Control,
    string G2,                    // R | RF | M | S | NA | O
    string Blk,                   // MAIN | AID
    IReadOnlyList<string> Why,    // GPA_LOW, SAT_BELOW, NEED_AID...
    double? Acc,                  // tỷ lệ nhận chung, 0–1
    int? Sat25,
    int? Sat75,
    double? Pg,
    double? Ps,
    double? Cost,
    double? Gap);

public record ExplainRequestDto(ExplainStudentDto Student, ExplainSchoolDto School);

public record ExplainResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,                       // OK | FAILED
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements);
