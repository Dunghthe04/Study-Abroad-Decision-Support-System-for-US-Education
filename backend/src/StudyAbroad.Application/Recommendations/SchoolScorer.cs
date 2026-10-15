namespace StudyAbroad.Application.Recommendations
{
    //Tầng 2 (hồ sơ mạnh đến đâu so với trường) và 3 tiêu chí F/H/Q của tầng 3, theo GOI_Y_TRUONG_TONG_QUAN.
    //Thay cho lượt ② AI: AI chỉ đúng 33–48% trên bộ đánh giá 216 cặp. Đáp án đối chiếu: advisor/evals/recommendation/reference.py.
    public static class SchoolScorer
    {
        public const decimal GpaSd = 0.3m;            // độ lệch chuẩn khi chỉ có GPA trung bình
        public const decimal LowSubmit = 0.5m;        // dưới 50% sinh viên nộp SAT thì L_SAT × 0,5
        public const decimal ReachBelow = 25m;        // S < 25: Khó vào
        public const decimal SafetyAbove = 75m;       // S > 75: An toàn hơn
        public const decimal IntlRatio = 0.5m;        // r = tỉ lệ nhận du học sinh / tỉ lệ chung < 0,5 thì hạ 1 nhóm
        public const decimal OpenAdmission = 0.99m;   // nhận ≥ 99%: không chia nhóm

        //Cận 7 khoảng GPA k1..k7: [0; 2,5) [2,5; 3) [3; 3,25) [3,25; 3,5) [3,5; 3,75) [3,75; 4) và đúng 4,0
        private static readonly decimal[] GpaEdges = [0m, 2.5m, 3.0m, 3.25m, 3.5m, 3.75m, 4.0m];

        //Bảng đổi z sang điểm vị trí (phân phối chuẩn), nội suy tuyến tính giữa 2 mốc
        private static readonly (decimal Z, decimal P)[] ZTable =
            [(-2m, 2m), (-1.5m, 7m), (-1m, 16m), (-0.5m, 31m), (0m, 50m), (0.5m, 69m), (1m, 84m), (1.5m, 93m), (2m, 98m)];

        //Chấm 1 trường cho 1 học sinh. Trả cùng khuôn với kết quả lượt ② AI để phần sau dùng chung.
        public static AiScoreItem Score(AiStudent student, AiSchool s)
        {
            decimal lg = s.Importance[0], ls = s.Importance[1], le = s.Importance[2];

            // Bước 1. Trọng số: ít người nộp SAT thì giảm; không dùng được SAT thì bằng 0
            if (s.SatSubmitPct is decimal subm && subm < LowSubmit) ls *= 0.5m;
            if (student.Sat is null || s.SatPolicy == "no" || s.Sat is null) ls = 0;

            // Bước 2. Điểm vị trí 0–100
            decimal? ps = null;
            if (ls > 0 && student.Sat is int sat && s.Sat is [var p25, var p75])
                ps = Clamp(25 + 50 * (sat - p25) / (decimal)(p75 - p25), 0, 100);

            decimal? pg = null;
            var conf = "L";
            if (student.Gpa is decimal gpa)
            {
                if (s.GpaBands is { Length: 7 } bands)
                {
                    pg = GpaPosition(gpa, bands);
                    conf = "H";
                }
                else if (s.AvgGpa4 is decimal avg)
                {
                    pg = ZToPosition((gpa - avg) / GpaSd);
                }
            }

            // Bước 3, 5, 6a. Điểm học thuật A, điều chỉnh ngoại khóa, S, nhóm
            decimal[] w = pg is null && ps is null ? [0.5m, 0.5m]
                : pg is null ? [0m, 1m]
                : ps is null ? [1m, 0m]
                : [lg / (lg + ls), ls / (lg + ls)];
            decimal? a = pg is null && ps is null ? null : w[0] * (pg ?? 0) + w[1] * (ps ?? 0);
            var adj = (student.Tier - 3) * le;            // bậc 3 là trung bình: hơn thì cộng, kém thì trừ
            var total = a + adj;                          // a null thì total null
            var g = (s.AcceptanceRate ?? 0) >= OpenAdmission ? "O"
                : total is null ? "NA"
                : total < ReachBelow ? "R"
                : total > SafetyAbove ? "S"
                : "M";

            // Bước 6b. Du học sinh: trường nhận ít du học sinh thì hạ 1 nhóm
            decimal? r = null;
            var g2 = g;
            if (s.IntlAcceptanceRate is decimal iacc && s.AcceptanceRate is decimal acc && acc > 0)
            {
                r = iacc / acc;
                if (r < IntlRatio)
                    g2 = g switch { "S" => "M", "M" => "R", "R" => "RF", _ => g };
            }

            // Tầng 3. Ba tiêu chí để xếp hạng
            decimal? coa = s.CostUsd, budget = student.Budget;
            decimal? f = budget > 0 && coa > 0 ? Clamp((budget.Value - coa.Value) / budget.Value, 0, 1) : null;
            decimal? h = s.IntlAidUsd is decimal aid && coa > 0 ? Math.Min(1m, aid / coa.Value) : null;
            decimal? gap = budget > 0 && coa > budget ? coa - budget : null;

            return new AiScoreItem(
                s.Code,
                [Math.Round(w[0], 2, MidpointRounding.AwayFromZero), Math.Round(w[1], 2, MidpointRounding.AwayFromZero)],
                Round(pg, 2), conf, Round(ps, 2), Round(a, 2), adj, Round(total, 2),
                g, Round(r, 2), g2,
                Round(f, 3), Round(h, 3), s.Quality, Round(gap, 0));
        }

        public static decimal GpaPosition(decimal gpa, decimal[] bands)
        {
            if (gpa >= 4.0m) return 100 * bands[..6].Sum() + 50 * bands[6];   // đúng 4,0: đứng giữa nhóm 4,0
            for (var k = 0; k < 6; k++)
            {
                decimal lo = GpaEdges[k], hi = GpaEdges[k + 1];
                if (gpa < hi)
                    return 100 * bands[..k].Sum() + 100 * Math.Max(0, (gpa - lo) / (hi - lo)) * bands[k];
            }
            return 100;   // không tới được (gpa < 4,0 luôn rơi vào k1..k6)
        }

        public static decimal ZToPosition(decimal z)
        {
            if (z <= ZTable[0].Z) return ZTable[0].P;
            if (z >= ZTable[^1].Z) return ZTable[^1].P;
            for (var i = 0; i < ZTable.Length - 1; i++)
            {
                var (z1, p1) = ZTable[i];
                var (z2, p2) = ZTable[i + 1];
                if (z <= z2) return p1 + (p2 - p1) * (z - z1) / (z2 - z1);
            }
            return ZTable[^1].P;
        }

        private static decimal Clamp(decimal x, decimal lo, decimal hi) => Math.Max(lo, Math.Min(hi, x));

        private static decimal? Round(decimal? x, int digits) =>
            x is decimal v ? Math.Round(v, digits, MidpointRounding.AwayFromZero) : null;
    }
}
