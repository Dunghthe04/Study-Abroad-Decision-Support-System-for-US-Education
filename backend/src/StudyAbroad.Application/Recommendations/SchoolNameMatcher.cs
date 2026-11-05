using System.Globalization;
using System.Text;

namespace StudyAbroad.Application.Recommendations;

// Khớp tên trường học sinh nói ("MIT", "boston uni", "Penn State") với danh sách trường trong hệ thống.
// SLM chỉ trích đoạn chữ; việc chọn đúng trường cần chắc chắn nên làm bằng code, dùng chung cho mọi câu hỏi về một trường.
// Found chỉ khi chắc chắn: trùng tên hoặc mã, chữ cái đầu duy nhất, tên bắt đầu bằng nguyên các từ đã gõ, hoặc gõ sai nhẹ.
// Ambiguous là cần học sinh chọn hoặc xác nhận: nhiều ứng viên, hoặc chỉ một ứng viên nhưng chữ đã gõ chỉ khớp dở từ hoặc ở giữa tên
// (gõ "Penn" thì hỏi "Pennsylvania State hay University of Pennsylvania?"; gõ "Washington University" mà chỉ có "George Washington University" thì hỏi lại).
// Cách gọi thân thuộc không suy ra được từ tên (BU, Penn, Cal) do SLM chuẩn hóa thành tên đầy đủ khi đã được huấn luyện, chưa thì hệ thống hỏi lại.
// NotFound khi không thấy.
// Không bao giờ đoán bừa một trường.

public record SchoolRef(string Code, string Name);

public enum NameMatchKind
{
    Found,
    Ambiguous,
    NotFound
}

// Schools: Found có đúng một trường; Ambiguous có từ một đến MaxCandidates trường để hỏi lại; NotFound rỗng.
public record NameMatch(NameMatchKind Kind, IReadOnlyList<SchoolRef> Schools);

public static class SchoolNameMatcher
{
    public const int MaxCandidates = 5;

    // Câu quá chung chung ("university", "college") khớp hàng chục trường: coi như chưa nêu được trường nào
    private const int TooManyHits = 20;

    // Từ nối trong tên trường, bỏ khi ghép chữ cái đầu ("University of California" → UC) và khi so từng từ
    private static readonly HashSet<string> Joiners = ["of", "the", "at", "in", "and", "for"];

    // Từ học sinh hay thêm vào ("đại học Boston", "trường MIT"), không thuộc tên trường
    private static readonly HashSet<string> Filler = ["truong", "dai", "hoc"];

    public static NameMatch Match(string? query, IReadOnlyList<SchoolRef> schools)
    {
        var q = StripThe(Normalize(query));
        foreach (var word in Filler)
            q = string.Join(' ', q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t != word));
        if (q.Length < 2)
            return NotFound;

        var index = schools.Select(s => new Indexed(s, StripThe(Normalize(s.Name)), Normalize(s.Code))).ToList();

        // 1. Trùng hẳn tên hoặc mã
        var exact = index.Where(i => i.Name == q || i.Code == q).ToList();
        if (exact.Count > 0)
            return Pick(exact);

        // 2. Chữ cái đầu ("MIT", "UCLA", "NYU"): chỉ nhận khi duy nhất
        if (!q.Contains(' ') && q.Length >= 2)
        {
            var byAcronym = index.Where(i => Acronym(i.Name) == q).ToList();
            if (byAcronym.Count > 0)
                return Pick(byAcronym);
        }

        if (q.Length < 3)
            return NotFound;   // hai ký tự chỉ có nghĩa khi là tên gọi thân thuộc hoặc chữ cái đầu ở trên

        // 3. Trùng chữ theo thứ tự: bắt đầu bằng cụm, rồi chứa cụm, rồi chứa đủ các từ (cho phép gõ tắt đầu từ: "univ")
        var tiers = new Func<Indexed, bool>[]
        {
            i => (i.Name + " ").StartsWith(q + " "),         // nguyên từ ở đầu tên: "boston" → Boston University
            i => i.Name.StartsWith(q),                        // dở từ ở đầu tên: "penn" → Pennsylvania...: phải xác nhận
            i => (" " + i.Name + " ").Contains(" " + q + " "),
            i => ContainsAllWords(i, q),
        };
        var tierHits = tiers.Select(t => index.Where(t).ToList()).ToList();
        var first = tierHits.FindIndex(h => h.Count > 0);
        if (first >= 0)
        {
            // Gom ứng viên từ mọi tầng, mỗi trường một lần, tầng tốt hơn trước: gõ "Penn" thì thấy cả Pennsylvania State
            // (dở từ ở đầu tên) lẫn University of Pennsylvania (chứa chữ ở giữa), không chỉ tầng đầu tiên có kết quả.
            var ordered = tierHits
                .SelectMany((hits, tier) => hits.Select(h => (Item: h, Tier: tier)))
                .GroupBy(x => x.Item.School.Code)
                .Select(g => g.OrderBy(x => x.Tier).First())
                .OrderBy(x => x.Tier).ThenBy(x => x.Item.Name.Length).ThenBy(x => x.Item.Name, StringComparer.Ordinal)
                .ToList();
            if (ordered.Count > TooManyHits)
                return NotFound;
            var candidates = ordered.Select(x => x.Item.School).ToList();
            // Tự chọn chỉ khi cụm khớp nguyên từ ở đầu tên và không còn ứng viên nào khác
            return candidates.Count == 1 && first == 0
                ? new NameMatch(NameMatchKind.Found, candidates)
                : new NameMatch(NameMatchKind.Ambiguous, candidates.Take(MaxCandidates).ToList());
        }

        // 4. Gõ sai nhẹ ("harvad", "standford"): chọn trường gần nhất nếu duy nhất
        return Fuzzy(index, q);
    }

    private static readonly NameMatch NotFound = new(NameMatchKind.NotFound, []);

    private record Indexed(SchoolRef School, string Name, string Code);

    // Một trường thì Found, nhiều thì Ambiguous. Ứng viên xếp theo tên ngắn trước (gần với điều học sinh gõ nhất)
    private static NameMatch Pick(List<Indexed> hits)
    {
        if (hits.Count > TooManyHits)
            return NotFound;
        var ordered = hits.OrderBy(h => h.Name.Length).ThenBy(h => h.Name, StringComparer.Ordinal).Select(h => h.School).ToList();
        return ordered.Count == 1
            ? new NameMatch(NameMatchKind.Found, ordered)
            : new NameMatch(NameMatchKind.Ambiguous, ordered.Take(MaxCandidates).ToList());
    }

    private static bool ContainsAllWords(Indexed school, string q)
    {
        var words = q.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Joiners.Contains(w)).ToList();
        if (words.Count == 0)
            return false;
        var nameWords = school.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Từ của học sinh là đầu một từ trong tên ("univ" khớp "university", "bos" khớp "boston")
        return words.All(w => nameWords.Any(n => n.StartsWith(w, StringComparison.Ordinal)));
    }

    // Sai tối đa 1 ký tự với từ từ 5 ký tự, 2 ký tự với từ từ 10 ký tự trở lên. So với cả tên và với phần đầu tên có cùng số từ.
    private static NameMatch Fuzzy(List<Indexed> index, string q)
    {
        var limit = q.Length >= 10 ? 2 : q.Length >= 5 ? 1 : 0;
        if (limit == 0)
            return NotFound;

        var qWords = q.Split(' ').Length;
        var scored = index
            .Select(i =>
            {
                var head = string.Join(' ', i.Name.Split(' ').Take(qWords));
                return (Item: i, Distance: Math.Min(Distance(q, i.Name, limit), Distance(q, head, limit)));
            })
            .Where(x => x.Distance <= limit)
            .ToList();
        if (scored.Count == 0)
            return NotFound;

        var best = scored.Min(x => x.Distance);
        return Pick(scored.Where(x => x.Distance == best).Select(x => x.Item).ToList());
    }

    // "The University of Texas at Austin" và "University of Texas at Austin" là một: bỏ "the" ở đầu khi so sánh
    private static string StripThe(string normalized) => normalized.StartsWith("the ") ? normalized[4..] : normalized;

    // Chữ cái đầu của các từ có nghĩa: "Massachusetts Institute of Technology" → "mit"
    private static string Acronym(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => !Joiners.Contains(w)).Select(w => w[0]));

    // Chữ thường, bỏ dấu, dấu câu thành khoảng trắng, "&" thành "and", gộp khoảng trắng
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";
        var decomposed = text.Replace("&", " and ").Replace("đ", "d").Replace("Đ", "d").ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Khoảng cách chỉnh sửa, dừng sớm khi vượt giới hạn
    private static int Distance(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
            return limit + 1;
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            var rowMin = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
                rowMin = Math.Min(rowMin, current[j]);
            }
            if (rowMin > limit)
                return limit + 1;
            previous = current;
        }
        return previous[b.Length];
    }
}
