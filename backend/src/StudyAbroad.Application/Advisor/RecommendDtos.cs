using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Advisor;

// Khuôn JSON trao đổi với advisor Python: viết lời tư vấn từng trường.
// Nhóm và thứ tự trường do code backend tính (SchoolGrouper, SchoolRanker), không qua advisor.
// Tên viết tắt giữ nguyên key bên Python. Key hoa (L, A, S, F, H, Q, X) phải gắn JsonPropertyName
// vì camelCase sẽ biến chúng thành chữ thường.

// --- Lời cho what-if trong chat: POST api/v1/recommend/what-if ---
// Kết quả simulate đã được code tính. Advisor (Gemini) viết lời và chặn tên trường, nhóm, số không có trong dữ liệu này.

// Một trường nêu trong nhóm. MovedFrom có nghĩa là trường vừa đổi nhóm (tên nhóm cũ, tiếng Việt); Reasons là mã lý do, chỉ gắn cho trường đã đổi nhóm.
public record ScenarioSchoolDto(string Name, string? MovedFrom, IReadOnlyList<string> Reasons, bool NeedAid);

// Key: reach | match | safety | aid. Schools tối đa 4 trường (như trang gợi ý), More là số trường còn lại của nhóm.
public record ScenarioGroupDto(string Key, string Title, IReadOnlyList<ScenarioSchoolDto> Schools, int More);

public record ScenarioStudentDto(int? BudgetUsd, string? Scholarship);

// Direction: better | worse | mixed | none (không trường nào đổi nhóm, khi đó Groups rỗng).
// Others: tên các trường khác trong kết quả, để advisor chặn lời nhắc tên ngoài danh sách.
public record ScenarioNoteRequestDto(
    string Label,
    IReadOnlyList<string> Factors,
    IReadOnlyList<string> Unsupported,
    string Direction,
    ScenarioStudentDto Student,
    IReadOnlyList<ScenarioGroupDto> Groups,
    IReadOnlyList<string> Others);

public record ScenarioNoteResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,                       // OK | FAILED
    string Answer);

// --- Lời cho goal-seeking trong chat: POST api/v1/recommend/goal-seek ---
// Kết quả goal-seeking đã được code tính. Advisor (Gemini) diễn đạt lại thành lời, chặn số, tên trường, lời hứa không có trong dữ kiện.

// School: tên trường hệ thống đã hiểu (học sinh thấy để sửa nếu nhầm). Target: tên nhóm muốn đạt. Outcome: already_reached | reachable | blocked | no_data.
// Facts: dữ kiện do code tính, mỗi câu một ý. Alternatives: trường tương tự. Others: tên các trường khác, để advisor chặn nhắc tên ngoài danh sách.
public record GoalNoteRequestDto(
    string School,
    string Target,
    string Outcome,
    IReadOnlyList<string> Facts,
    IReadOnlyList<string> Alternatives,
    IReadOnlyList<string> Others);

public record GoalNoteResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,                       // OK | FAILED
    string Answer);

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

// Basis: giả định học sinh đang xem ("GPA 3.6, SAT 1500"); null là hồ sơ thật. Số liệu của Student và School đã tính theo gốc này
public record ExplainRequestDto(ExplainStudentDto Student, ExplainSchoolDto School, string? Basis = null);

public record ExplainResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,                       // OK | FAILED
    string Summary,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Improvements);
