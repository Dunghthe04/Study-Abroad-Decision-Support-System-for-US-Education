using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    //Gọi advisor (Python) cho phần cần AI: đọc mô tả hoạt động ngoại khóa. Khuôn JSON khớp advisor/app/schemas/ec.py.
    public interface IRecommendationAi
    {
        //AI đọc vai trò, phạm vi từng hoạt động; advisor tính q, m và bậc ngoại khóa 1–5
        Task<AiEcResult> ReadExtracurricularAsync(AiEcRequest request, CancellationToken ct = default);

        //AI viết lời tư vấn cho 1 trường từ số liệu đã chấm (advisor /recommend/explain)
        Task<AiExplainResult> ExplainAsync(AiExplainRequest request, CancellationToken ct = default);
    }

    //Advisor không trả được kết quả: tắt, quá thời gian, lỗi HTTP, hoặc JSON không đọc được
    public class RecommendationAiException(string message, Exception? inner = null) : Exception(message, inner);

    // ---------- Bậc ngoại khóa (advisor /recommend/ec) ----------

    public record AiEcActivity(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("scope")] int Scope,      // phạm vi học sinh chọn: 1 trường ... 5 quốc tế
        [property: JsonPropertyName("months")] int Months,
        [property: JsonPropertyName("role")] string? Role = null);   // vai trò chọn ở form: founder | leader | vice_leader | member (dùng khi AI lỗi)

    public record AiEcAward(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("level")] int Level);     // cấp giải, cùng thang với scope

    public record AiEcRequest(
        [property: JsonPropertyName("act")] IReadOnlyList<AiEcActivity> Activities,
        [property: JsonPropertyName("awards")] IReadOnlyList<AiEcAward> Awards);

    public record AiEcActivityResult(
        [property: JsonPropertyName("role")] decimal Role,    // 1.0 sáng lập, 0.8 trưởng, 0.6 phó, 0.3 thành viên
        [property: JsonPropertyName("scope")] int Scope,      // phạm vi sau khi AI xét (chỉ giữ hoặc hạ)
        [property: JsonPropertyName("q")] decimal Q);         // điểm hoạt động, Python tính

    public record AiEcResult(
        [property: JsonPropertyName("act")] IReadOnlyList<AiEcActivityResult> Activities,
        [property: JsonPropertyName("tong_hd")] decimal ActivityTotal,
        [property: JsonPropertyName("bonus")] decimal Bonus,
        [property: JsonPropertyName("m")] decimal M,
        [property: JsonPropertyName("tier")] int Tier,
        [property: JsonPropertyName("skill_version")] string SkillVersion,
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("ai_used")] bool AiUsed = true);   // false = AI lỗi, vai trò lấy từ form

    // ---------- Tư vấn từng trường (advisor /recommend/explain) ----------

    public record AiExplainStudent(
        [property: JsonPropertyName("gpa")] decimal? Gpa,
        [property: JsonPropertyName("sat")] int? Sat,
        [property: JsonPropertyName("ielts")] decimal? Ielts,
        [property: JsonPropertyName("toefl")] decimal? Toefl,
        [property: JsonPropertyName("duolingo")] decimal? Duolingo,
        [property: JsonPropertyName("budget")] decimal? Budget,
        [property: JsonPropertyName("ec_tier")] int? EcTier,
        [property: JsonPropertyName("major")] string? Major);

    public record AiExplainSchool(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("control")] string? Control,
        [property: JsonPropertyName("group")] string Group,          // Khó vào | Khó vào xa | Vừa sức | An toàn hơn | Chưa đủ dữ liệu
        [property: JsonPropertyName("need_aid")] bool NeedAid,
        [property: JsonPropertyName("acceptance")] decimal? AcceptancePercent,
        [property: JsonPropertyName("avg_gpa")] decimal? AvgGpa,
        [property: JsonPropertyName("sat25")] int? Sat25,
        [property: JsonPropertyName("sat75")] int? Sat75,
        [property: JsonPropertyName("gpa_position")] decimal? GpaPosition,
        [property: JsonPropertyName("sat_position")] decimal? SatPosition,
        [property: JsonPropertyName("cost")] decimal? Cost,
        [property: JsonPropertyName("over_budget")] decimal? OverBudget,
        [property: JsonPropertyName("ec_adjustment")] decimal? EcAdjustment);

    public record AiExplainRequest(
        [property: JsonPropertyName("student")] AiExplainStudent Student,
        [property: JsonPropertyName("school")] AiExplainSchool School);

    public record AiExplainResult(
        [property: JsonPropertyName("summary")] string Summary,
        [property: JsonPropertyName("strengths")] IReadOnlyList<string> Strengths,
        [property: JsonPropertyName("improvements")] IReadOnlyList<string> Improvements,
        [property: JsonPropertyName("skill_version")] string SkillVersion,
        [property: JsonPropertyName("model")] string Model);

    // ---------- Chấm trường (SchoolScorer) ----------
    // Tên key ngắn giữ giống bộ đánh giá advisor/evals/recommendation/cases.json để test đối chiếu được.

    public record AiStudent(
        [property: JsonPropertyName("gpa")] decimal? Gpa,
        [property: JsonPropertyName("sat")] int? Sat,
        [property: JsonPropertyName("budget")] decimal? Budget,
        [property: JsonPropertyName("tier")] int Tier);       // bậc ngoại khóa

    public record AiScoreItem(
        [property: JsonPropertyName("c")] string Code,
        [property: JsonPropertyName("w")] decimal[] W,          // trọng số [GPA, SAT]
        [property: JsonPropertyName("pg")] decimal? Pg,         // điểm vị trí GPA 0–100
        [property: JsonPropertyName("conf")] string Conf,       // H = có phân phối C11 | L = ước lượng
        [property: JsonPropertyName("ps")] decimal? Ps,         // điểm vị trí SAT 0–100
        [property: JsonPropertyName("A")] decimal? A,           // điểm học thuật
        [property: JsonPropertyName("adj")] decimal Adj,        // điều chỉnh ngoại khóa −6..+6
        [property: JsonPropertyName("S")] decimal? S,           // điểm sức mạnh = A + adj
        [property: JsonPropertyName("g")] string G,             // nhóm theo S
        [property: JsonPropertyName("r")] decimal? R,           // tỉ lệ nhận du học sinh / tỉ lệ chung
        [property: JsonPropertyName("g2")] string G2,           // nhóm cuối: R | RF | M | S | NA | O
        [property: JsonPropertyName("F")] decimal? F,           // tài chính 0–1
        [property: JsonPropertyName("H")] decimal? H,           // học bổng 0–1
        [property: JsonPropertyName("Q")] decimal? Q,           // chất lượng 0–1
        [property: JsonPropertyName("gap")] decimal? Gap);      // số tiền vượt ngân sách

    // ---------- Chia khối, xếp hạng (SchoolPicker) ----------

    public record AiPickItem(
        [property: JsonPropertyName("c")] string Code,
        [property: JsonPropertyName("g2")] string G2,
        [property: JsonPropertyName("blk")] string Block,     // MAIN | AID (từ HardFilter)
        [property: JsonPropertyName("F")] decimal? F,
        [property: JsonPropertyName("H")] decimal? H,
        [property: JsonPropertyName("Q")] decimal? Q,
        [property: JsonPropertyName("gap")] decimal? Gap,
        [property: JsonPropertyName("pg")] decimal? Pg,
        [property: JsonPropertyName("ps")] decimal? Ps,
        [property: JsonPropertyName("adj")] decimal? Adj,
        [property: JsonPropertyName("r")] decimal? R);

    public record AiPickRequest(
        [property: JsonPropertyName("pref")] int[] Pref,      // [chi phí, học bổng, chất lượng], mỗi mức 0–4
        [property: JsonPropertyName("needAid")] string NeedAid,   // no | preferred | required
        [property: JsonPropertyName("items")] IReadOnlyList<AiPickItem> Items);

    public record AiPickEntry(
        [property: JsonPropertyName("c")] string Code,
        [property: JsonPropertyName("X")] decimal? X,         // điểm xếp hạng; khối aid và other không có
        [property: JsonPropertyName("why")] IReadOnlyList<string> Why);

    public record AiPickResult(
        [property: JsonPropertyName("reach")] IReadOnlyList<AiPickEntry> Reach,
        [property: JsonPropertyName("match")] IReadOnlyList<AiPickEntry> Match,
        [property: JsonPropertyName("safety")] IReadOnlyList<AiPickEntry> Safety,
        [property: JsonPropertyName("aid")] IReadOnlyList<AiPickEntry> Aid,
        [property: JsonPropertyName("other")] IReadOnlyList<AiPickEntry> Other,
        [property: JsonPropertyName("skill_version")] string SkillVersion,
        [property: JsonPropertyName("model")] string Model);
}
