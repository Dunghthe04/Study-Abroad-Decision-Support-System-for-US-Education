using System.Text.Json;
using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Recommendations
{
    //Cách đổi object sang JSON khi gửi advisor: camelCase, bỏ hẳn key có giá trị null
    public static class AiJson
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    //Một trường ở dạng JSON gọn gửi cho advisor (lượt ②). Key ngắn để tiết kiệm token, đúng mục 4 file AI_CACH_THUC_HIEN.
    //Thiếu số thì để null; khi gửi đi null bị bỏ hẳn khỏi JSON (skill: key vắng mặt = thiếu dữ liệu).
    public record AiSchool(
        [property: JsonPropertyName("c")] string Code,
        [property: JsonPropertyName("acc")] decimal? AcceptanceRate,
        [property: JsonPropertyName("iacc")] decimal? IntlAcceptanceRate,
        [property: JsonPropertyName("sat")] int[]? Sat,              // [mốc 25, mốc 75]
        [property: JsonPropertyName("subm")] decimal? SatSubmitPct,
        [property: JsonPropertyName("satp")] string? SatPolicy,      // req | opt | no
        [property: JsonPropertyName("gpa")] decimal[]? GpaBands,     // 7 khoảng, từ THẤP lên CAO (k1..k7)
        [property: JsonPropertyName("avg")] decimal? AvgGpa4,
        [property: JsonPropertyName("L")] int[] Importance,           // C7 [GPA, SAT, ngoại khóa], 0–3
        [property: JsonPropertyName("coa")] decimal? CostUsd,
        [property: JsonPropertyName("aid")] decimal? IntlAidUsd,
        [property: JsonPropertyName("q")] decimal? Quality);         // TB tỉ lệ giữ chân và tốt nghiệp

    public static class AiSchoolMapper
    {
        public const string C7DefaultFlag = "C7_DEFAULT";    // trường thiếu C7, đã điền mức mặc định

        //Mức C7 mặc định khi trường không công bố (file tổng quan, tầng 2 bước 1)
        private static readonly int[] SelectiveDefault = [3, 2, 2];   // tư thục hoặc tỉ lệ nhận < 50%
        private static readonly int[] PublicDefault = [2, 2, 1];      // công lập ít chọn lọc

        public static int? ToLevel(string? importance) => importance?.Trim().ToLowerInvariant() switch
        {
            "very_important" => 3,
            "important" => 2,
            "considered" => 1,
            "not_considered" => 0,
            _ => null
        };

        //Đổi SchoolCandidate (đọc từ DB) sang JSON gọn. flags: cờ dữ liệu để hiện ở màn 9.
        public static AiSchool ToAi(SchoolCandidate s, out IReadOnlyList<string> flags)
        {
            var list = new List<string>();

            // C7: chữ → số; thiếu mục nào thì điền mức mặc định của mục đó và gắn cờ
            var selective = string.Equals(s.Control, "private", StringComparison.OrdinalIgnoreCase) || s.AcceptanceRate < 0.5m;
            var defaults = selective ? SelectiveDefault : PublicDefault;
            int?[] levels = [ToLevel(s.C7GpaImportance), ToLevel(s.C7TestImportance), ToLevel(s.C7EcImportance)];
            if (levels.Any(l => l is null)) list.Add(C7DefaultFlag);
            var importance = levels.Select((l, i) => l ?? defaults[i]).ToArray();

            // SAT chỉ gửi khi có đủ 2 mốc hợp lệ
            int[]? sat = s.Sat25 is int lo && s.Sat75 is int hi && lo < hi ? [lo, hi] : null;

            // Chất lượng = trung bình các tỉ lệ đang có
            decimal?[] rates = [s.RetentionRate1Yr, s.GradRate6Yr];
            var known = rates.Where(r => r is not null).Select(r => r!.Value).ToList();
            decimal? quality = known.Count > 0 ? Math.Round(known.Average(), 3) : null;

            flags = list;
            return new AiSchool(
                s.Code,
                Trim(s.AcceptanceRate),
                Trim(s.IntlAcceptanceRate),
                sat,
                Trim(s.SatSubmitPct),
                SatPolicyCode(s.SatPolicy),
                GpaBandsLowToHigh(s.GpaDistribution),
                Trim(s.AvgGpa4),
                importance,
                Trim(s.TotalCostUsd),
                Trim(s.IntlAidAvgUsd),
                Trim(quality));
        }

        private static string? SatPolicyCode(string? policy) => policy switch
        {
            "required" => "req",
            "optional" => "opt",
            "not_accepted" => "no",
            _ => null
        };

        //DB lưu từ cao xuống thấp (4.0 trước); skill cần từ THẤP lên CAO (k1 = dưới 2.50 ... k7 = 4.0).
        //Thiếu bất kỳ khoảng nào thì coi như không có phân phối (skill dùng GPA trung bình thay thế).
        private static decimal[]? GpaBandsLowToHigh(GpaDistribution? d)
        {
            if (d is null) return null;
            decimal?[] bands = [d.PctBelow250, d.Pct250To299, d.Pct300To324, d.Pct325To349, d.Pct350To374, d.Pct375To399, d.Pct40];
            return bands.Any(b => b is null) ? null : bands.Select(b => Trim(b)!.Value).ToArray();
        }

        //Bỏ số 0 thừa sau dấu phẩy: DB trả 0.1750 (numeric(5,4)) → gửi 0.175, đỡ token và dễ đọc
        private static decimal? Trim(decimal? value) => value / 1.000000000000000000000000000000000m;
    }
}
