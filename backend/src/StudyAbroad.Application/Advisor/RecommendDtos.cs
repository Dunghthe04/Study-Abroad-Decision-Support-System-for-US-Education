using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Advisor;

// Khuôn JSON trao đổi với advisor Python: đọc ngoại khóa và viết lời tư vấn từng trường.
// Nhóm và thứ tự trường do code backend tính (SchoolGrouper, SchoolRanker), không qua advisor.
// Tên viết tắt giữ nguyên key bên Python. Key hoa (L, A, S, F, H, Q, X) phải gắn JsonPropertyName
// vì camelCase sẽ biến chúng thành chữ thường.

// --- Ngoại khóa: POST api/v1/recommend/ec ---

// Một hoạt động học sinh khai.
public record EcActivityDto(
    string Text,   // mô tả, tối đa 500 ký tự
    int Scope,     // phạm vi 1 trường … 5 quốc tế
    int Months,    // số tháng tham gia, 0–120
    string? Role); // founder | leader | vice_leader | member; trống thì AI tự đọc

// Một giải thưởng.
public record EcAwardDto(
    string Text, // mô tả, tối đa 300 ký tự
    int Level);  // cấp giải, cùng thang 1–5 với Scope

// Đầu vào lượt đọc ngoại khóa. Mỗi mảng tối đa 10 phần tử.
public record EcRequestDto(IReadOnlyList<EcActivityDto> Act, IReadOnlyList<EcAwardDto> Awards);

// Kết quả AI đọc một hoạt động, kèm điểm q do Python tính.
public record EcActivityResultDto(
    double Role, // founder 1.0 | leader 0.8 | vice_leader 0.6 | member 0.3
    int Scope,   // phạm vi 1–5
    double Q);   // điểm hoạt động

public record EcResponseDto(
    IReadOnlyList<EcActivityResultDto> Act,
    [property: JsonPropertyName("tong_hd")] double TongHd, // tổng điểm hoạt động
    double Bonus,                                            // tổng điểm giải thưởng
    int Tier,                                                // bậc ngoại khóa 1–5, đưa sang lượt chấm
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    [property: JsonPropertyName("ai_used")] bool AiUsed);

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
