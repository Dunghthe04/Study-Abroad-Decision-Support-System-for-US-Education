using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Advisor;

// Khuôn JSON trao đổi với advisor Python: ngoại khóa → chấm trường → chọn danh sách.
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

// --- Chấm trường: POST api/v1/recommend/score ---

// Hồ sơ học sinh đưa vào lượt chấm.
public record ScoreStudentDto(
    double? Gpa,    // GPA thang 4.0
    int? Sat,       // SAT 400–1600
    double? Budget, // ngân sách USD/năm
    int Tier);      // bậc ngoại khóa đã tính ở lượt trước

// Một trường đã qua lọc cứng. Thiếu số thì để null, không gửi 0.
public record ScoreSchoolDto(
    string C,                    // mã trường, chép nguyên
    double? Acc,                 // tỷ lệ nhận chung, 0–1
    double? Iacc,                // tỷ lệ nhận du học sinh, 0–1
    IReadOnlyList<int>? Sat,     // [mốc SAT 25, mốc SAT 75]
    double? Subm,                // tỷ lệ sinh viên nộp SAT, 0–1
    string? Satp,                // chính sách SAT: req | opt | no
    IReadOnlyList<double>? Gpa,  // 7 tỷ lệ GPA trúng tuyển, từ thấp đến 4.0
    double? Avg,                 // GPA trung bình của trường
    [property: JsonPropertyName("L")] IReadOnlyList<int> L, // [mức GPA, mức SAT, mức ngoại khóa]: 3 rất quan trọng … 0 không xét
    double? Coa,                 // tổng chi phí USD/năm
    double? Aid,                 // học bổng trung bình mỗi du học sinh, USD/năm
    double? Q);                  // chất lượng đào tạo, 0–1

// Đầu vào lượt chấm: một học sinh và danh sách trường.
public record ScoreRequestDto(ScoreStudentDto Student, IReadOnlyList<ScoreSchoolDto> Schools);

// Kết quả chấm một trường.
public record ScoreItemDto(
    string C,                              // mã trường
    IReadOnlyList<double> W,               // trọng số [GPA, SAT]
    double? Pg,                            // điểm vị trí GPA, 0–100
    string Conf,                           // độ tin cậy GPA: H (có phân bố) | L (ước từ trung bình hoặc thiếu)
    double? Ps,                            // điểm vị trí SAT, 0–100
    [property: JsonPropertyName("A")] double? A, // điểm học thuật = wGpa×pg + wSat×ps
    double Adj,                            // cộng/trừ ngoại khóa, trong −6..+6
    [property: JsonPropertyName("S")] double? S, // sức mạnh hồ sơ = A + adj, dùng để chia nhóm
    string G,                              // nhóm theo sức mạnh: R khó | M vừa | S an toàn | NA thiếu điểm | O nhận gần như tất cả
    double? R,                             // iacc/acc; < 0.5 thì hạ một bậc sang G2
    string G2,                             // nhóm sau khi xét tỷ lệ du học sinh: R | RF | M | S | NA | O
    [property: JsonPropertyName("F")] double? F, // độ dư ngân sách (budget − coa) / budget, kẹp 0–1
    [property: JsonPropertyName("H")] double? H, // học bổng / chi phí, tối đa 1
    [property: JsonPropertyName("Q")] double? Q, // chất lượng, chép từ đầu vào
    double? Gap);                          // USD còn thiếu khi chi phí > ngân sách; trong ngân sách thì null

// Kết quả lượt chấm. Học sinh không nằm trong phản hồi.
public record ScoreResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    IReadOnlyList<ScoreItemDto> Items, // từng trường đã chấm, đúng số lượng đã gửi
    IReadOnlyList<string> Failed);     // mã trường chấm lỗi

// --- Chọn danh sách: POST api/v1/recommend/pick ---

// Một trường đã chấm, đưa vào lượt chọn. Không đổi G2 ở lượt này.
public record PickSchoolDto(
    string C,
    string G2,   // R | RF | M | S | NA | O
    string Blk,  // MAIN trong ngân sách | AID cần học bổng
    [property: JsonPropertyName("F")] double? F,
    [property: JsonPropertyName("H")] double? H,
    [property: JsonPropertyName("Q")] double? Q,
    double? Gap,
    double? Pg,
    double? Ps,
    double? Adj,
    double? R);

// Ưu tiên của học sinh và danh sách trường đã chấm.
public record PickRequestDto(
    IReadOnlyList<int> Pref, // [chi phí, học bổng, chất lượng], mỗi mức 0–4
    [property: JsonPropertyName("needAid")] string NeedAid, // no | preferred | required
    IReadOnlyList<PickSchoolDto> Items);

// Một trường được xếp vào khối.
public record PickEntryDto(
    string C,
    [property: JsonPropertyName("X")] double? X, // điểm xếp hạng; khối aid/other có thể null
    IReadOnlyList<string> Why); // GPA_HIGH/MID/LOW, SAT_ABOVE/IN_RANGE/BELOW, BUDGET_OK, NEED_AID, INTL_LOW, EC_PLUS/MINUS

// Năm khối đã cắt số lượng: reach ≤ 3, match ≤ 5, safety ≤ 4.
public record PickResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,                      // OK khi chọn xong
    IReadOnlyList<PickEntryDto> Reach,  // MAIN, G2 = R hoặc RF
    IReadOnlyList<PickEntryDto> Match,  // MAIN, G2 = M
    IReadOnlyList<PickEntryDto> Safety, // MAIN, G2 = S
    IReadOnlyList<PickEntryDto> Aid,    // blk AID, chỉ khi needAid khác no
    IReadOnlyList<PickEntryDto> Other); // G2 = NA hoặc O
