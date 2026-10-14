namespace StudyAbroad.Application.Recommendations
{
    //Dự phòng lượt ③ (mục 10 file AI_CACH_THUC_HIEN): AI chọn danh sách lỗi thì code làm đúng các bước P1–P5 của skill_pick.
    //Đây là luật cố định (tra nhóm, tính X, sắp xếp, cắt hạn mức, chọn mã lý do), không cần AI.
    public static class PickFallback
    {
        public const string Version = "code";

        public static AiPickResult Pick(AiPickRequest request, PerCategoryLimits limits)
        {
            var weights = Weights(request.Pref);

            // P3: chia khối theo nhóm g2 của lượt ② và khối AID của lọc cứng. Không đổi nhóm.
            var main = request.Items.Where(i => i.Block == "MAIN" && i.G2 is not ("NA" or "O")).ToList();
            var reach = Rank(main.Where(i => i.G2 is "R" or "RF"), weights, limits.Reach);
            var match = Rank(main.Where(i => i.G2 == "M"), weights, limits.Match);
            var safety = Rank(main.Where(i => i.G2 == "S"), weights, limits.Safety);

            // Khối học bổng: chỉ khi học sinh cần học bổng; trường vượt ít tiền đứng trước
            var aid = request.NeedAid == "no" ? [] : request.Items
                .Where(i => i.Block == "AID")
                .OrderBy(i => i.Gap ?? decimal.MaxValue).ThenBy(i => i.Code, StringComparer.Ordinal)
                .Select(i => new AiPickEntry(i.Code, null, Why(i)))
                .ToList();

            var other = request.Items
                .Where(i => i.Block == "MAIN" && i.G2 is ("NA" or "O"))   // thiếu dữ liệu hoặc trường nhận gần như tất cả
                .OrderBy(i => i.Code, StringComparer.Ordinal)
                .Select(i => new AiPickEntry(i.Code, null, Why(i)))
                .ToList();

            return new AiPickResult(reach, match, safety, aid, other, Version, Version);
        }

        // P1: mức quan tâm 0–4 → trọng số; cả 3 bằng 0 thì chia đều
        private static decimal[] Weights(int[] pref)
        {
            var total = pref.Sum();
            return total == 0 ? [1m / 3, 1m / 3, 1m / 3] : pref.Select(p => (decimal)p / total).ToArray();
        }

        // P2 + P4: X = w·[F, H, Q], tiêu chí thiếu thì bỏ và chia lại trọng số; sắp X giảm dần, bằng X thì mã tăng dần
        private static List<AiPickEntry> Rank(IEnumerable<AiPickItem> items, decimal[] w, int limit) => items
            .Select(i => (Item: i, X: RankScore(i, w)))
            .OrderByDescending(x => x.X ?? -1m)
            .ThenBy(x => x.Item.Code, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new AiPickEntry(x.Item.Code, x.X, Why(x.Item)))
            .ToList();

        public static decimal? RankScore(AiPickItem i, decimal[] w)
        {
            decimal?[] values = [i.F, i.H, i.Q];
            var used = values.Select((v, k) => (v, w: w[k])).Where(x => x.v is not null).ToList();
            var weightSum = used.Sum(x => x.w);
            if (used.Count == 0 || weightSum == 0) return null;
            return Math.Round(used.Sum(x => x.w * x.v!.Value) / weightSum, 3);
        }

        // P5: mã lý do theo đúng thứ tự, bỏ mục không có số
        public static IReadOnlyList<string> Why(AiPickItem i)
        {
            var why = new List<string>();
            if (i.Pg is decimal pg) why.Add(pg > 75 ? "GPA_HIGH" : pg >= 25 ? "GPA_MID" : "GPA_LOW");
            if (i.Ps is decimal ps) why.Add(ps > 75 ? "SAT_ABOVE" : ps >= 25 ? "SAT_IN_RANGE" : "SAT_BELOW");
            why.Add(i.Block == "AID" ? "NEED_AID" : "BUDGET_OK");
            if (i.R is < 0.5m) why.Add("INTL_LOW");
            else if (i.Adj > 0) why.Add("EC_PLUS");
            else if (i.Adj < 0) why.Add("EC_MINUS");
            return why;
        }
    }
}
