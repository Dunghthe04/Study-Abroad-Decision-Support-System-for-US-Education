using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
 
        //Gọi 3 lượt AI bên advisor (Python). Khuôn JSON phải khớp advisor/app/schemas/ec.py, score.py, pick.py.
        public interface IRecommendationAi
        {
            //Lượt ①: hoạt động + giải thưởng → bậc ngoại khóa 1–5
            Task<AiEcResult> ReadExtracurricularAsync(AiEcRequest request, CancellationToken ct = default);

            //Lượt ②: hồ sơ + các trường sau lọc cứng → điểm, nhóm, F/H/Q từng trường (advisor tự chia lô 8 trường)
            Task<AiScoreResult> ScoreAsync(AiScoreRequest request, CancellationToken ct = default);

            //Lượt ③: mức quan tâm + kết quả lượt ② → 4 khối, X, mã lý do
            Task<AiPickResult> PickAsync(AiPickRequest request, CancellationToken ct = default);

           
    }
    //Advisor không trả được kết quả: tắt, quá thời gian, lỗi 502 (AI trả sai 2 lần), hoặc JSON không đọc được
    public class RecommendationAiException(string message, Exception? inner = null) : Exception(message, inner);

    public record AiEcActivity(
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("scope")] int Scope,      // 1 trường ... 5 quốc tế
        [property: JsonPropertyName("months")] int Months);

        public record AiEcAward(
            [property: JsonPropertyName("text")] string Text,
            [property: JsonPropertyName("level")] int Level);     // cùng thang với scope

        public record AiEcRequest(
            [property: JsonPropertyName("act")] IReadOnlyList<AiEcActivity> Activities,
            [property: JsonPropertyName("awards")] IReadOnlyList<AiEcAward> Awards);

        public record AiEcActivityResult(
            [property: JsonPropertyName("role")] decimal Role,    // 1.0 sáng lập, 0.8 trưởng, 0.6 phó, 0.3 thành viên
            [property: JsonPropertyName("scope")] int Scope,      // phạm vi sau khi AI xét (chỉ giữ hoặc hạ)
            [property: JsonPropertyName("q")] decimal Q);

        public record AiEcResult(
            [property: JsonPropertyName("act")] IReadOnlyList<AiEcActivityResult> Activities,
            [property: JsonPropertyName("tong_hd")] decimal ActivityTotal,
            [property: JsonPropertyName("bonus")] decimal Bonus,
            [property: JsonPropertyName("m")] decimal M,
            [property: JsonPropertyName("tier")] int Tier,
            [property: JsonPropertyName("skill_version")] string SkillVersion,
            [property: JsonPropertyName("model")] string Model);


        // ---------- Lượt ② ----------

        public record AiStudent(
            [property: JsonPropertyName("gpa")] decimal? Gpa,
            [property: JsonPropertyName("sat")] int? Sat,
            [property: JsonPropertyName("budget")] decimal? Budget,
            [property: JsonPropertyName("tier")] int Tier); // bậc ngoại khóa

        public record AiScoreRequest(
            [property: JsonPropertyName("student")] AiStudent Student,
            [property: JsonPropertyName("schools")] IReadOnlyList<AiSchool> Schools);

        public record AiScoreItem(
            [property: JsonPropertyName("c")] string Code,
            [property: JsonPropertyName("w")] decimal[] W,
            [property: JsonPropertyName("pg")] decimal? Pg,
            [property: JsonPropertyName("conf")] string Conf,     // H | L
            [property: JsonPropertyName("ps")] decimal? Ps,
            [property: JsonPropertyName("A")] decimal? A,
            [property: JsonPropertyName("adj")] decimal Adj,
            [property: JsonPropertyName("S")] decimal? S,
            [property: JsonPropertyName("g")] string G,
            [property: JsonPropertyName("r")] decimal? R,
            [property: JsonPropertyName("g2")] string G2,         // R | RF | M | S | NA | O
            [property: JsonPropertyName("F")] decimal? F,
            [property: JsonPropertyName("H")] decimal? H,
            [property: JsonPropertyName("Q")] decimal? Q,
            [property: JsonPropertyName("gap")] decimal? Gap);

        public record AiScoreResult(
            [property: JsonPropertyName("skill_version")] string SkillVersion,
            [property: JsonPropertyName("model")] string Model,
            [property: JsonPropertyName("items")] IReadOnlyList<AiScoreItem> Items,
            [property: JsonPropertyName("failed")] IReadOnlyList<string> Failed);   // mã trường AI trả sai 2 lần

        // ---------- Lượt ③ ----------

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
            [property: JsonPropertyName("X")] decimal? X,
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
