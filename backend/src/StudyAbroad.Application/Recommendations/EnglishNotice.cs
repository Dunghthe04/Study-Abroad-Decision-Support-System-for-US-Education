namespace StudyAbroad.Application.Recommendations;

// Một nấc của bậc thang mở khóa: nếu học sinh đạt IELTS này (giữ nguyên các bài thi khác) thì có bao nhiêu trường đủ điều kiện nộp.
public record EnglishLadderStep(decimal Ielts, int Eligible);

// Thông báo tiếng Anh của một lượt gợi ý (B4).
// Hidden: số trường bị ẩn CHỈ vì tiếng Anh (vượt hết các bộ lọc khác nếu tiếng Anh đủ).
// NoCertificate: học sinh chưa có chứng chỉ tiếng Anh nào.
// Ladder: bậc thang mở khóa, chỉ gồm các mức làm tăng số trường đủ điều kiện.
public record EnglishSummary(int Hidden, bool NoCertificate, IReadOnlyList<EnglishLadderStep> Ladder);

// Dựng thông báo tiếng Anh từ kết quả lọc. Thuần code: không gọi mạng, không ghi database.
public static class EnglishNotice
{
    // Các mức IELTS đưa vào bậc thang
    public static readonly decimal[] LadderLevels = [5.0m, 5.5m, 6.0m, 6.5m, 7.0m, 7.5m];

    // Điểm giả định đủ cao để mọi mức tối thiểu của trường đều đạt: dùng để tách ra trường chỉ bị chặn vì tiếng Anh
    private const decimal TopIelts = 9m;
    private const decimal TopToefl = 120m;
    private const decimal TopDuolingo = 160m;

    // Trả null khi không có trường nào bị ẩn chỉ vì tiếng Anh (không cần thông báo).
    // filtered: kết quả lọc của mọi trường; schools: cùng danh sách trường đã đưa vào lọc.
    public static EnglishSummary? Build(
        FilterCriteria criteria, IReadOnlyList<FilterSchool> schools, IReadOnlyList<FilterResult> filtered,
        decimal budgetTolerance, EnglishOptions? options = null)
    {
        // Trường bị loại do tiếng Anh (đã qua ngành, bang, loại trường vì B4 đứng sau các bước đó)
        var excludedByEnglish = filtered
            .Where(r => r.Outcome == FilterOutcome.Excluded
                        && r.ExcludeReason is EnglishExclude.Short or EnglishExclude.NoCertificate)
            .ToList();
        if (excludedByEnglish.Count == 0) return null;

        // Chỉ đếm trường mà nếu tiếng Anh đủ thì vẫn qua các bộ lọc còn lại (ngân sách...)
        var strong = criteria with { Ielts = TopIelts, Toefl = TopToefl, Duolingo = TopDuolingo };
        var hidden = excludedByEnglish.Count(r =>
            HardFilter.Check(strong, r.School, budgetTolerance, options).Outcome != FilterOutcome.Excluded);
        if (hidden == 0) return null;

        var noCertificate = criteria.Ielts is null && criteria.Toefl is null && criteria.Duolingo is null;

        // Bậc thang: với mỗi mức IELTS cao hơn điểm hiện tại, số trường đủ điều kiện (giữ nguyên TOEFL, Duolingo của học sinh)
        // Chỉ ghi các nấc làm tăng thêm số trường so với nấc trước (và so với hiện tại), để khỏi lặp lại cùng một con số.
        var previous = filtered.Count(r => r.Outcome != FilterOutcome.Excluded);
        var ladder = new List<EnglishLadderStep>();
        foreach (var level in LadderLevels)
        {
            if (criteria.Ielts is decimal own && level <= own) continue;
            var withLevel = criteria with { Ielts = level };
            var eligible = schools.Count(s =>
                HardFilter.Check(withLevel, s, budgetTolerance, options).Outcome != FilterOutcome.Excluded);
            if (eligible <= previous) continue;
            ladder.Add(new EnglishLadderStep(level, eligible));
            previous = eligible;
        }

        return new EnglishSummary(hidden, noCertificate, ladder);
    }
}
