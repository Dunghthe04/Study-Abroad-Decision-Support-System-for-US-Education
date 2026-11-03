namespace StudyAbroad.Application.Recommendations;

// Nhóm của một trường đối với một học sinh. NoData = chưa đủ dữ liệu để xếp.
// Thứ tự từ thấp lên cao theo mức dễ vào, để "hạ một nhóm" chỉ là trừ một.
public enum SchoolGroup
{
    NoData,
    Reach,   // Khó vào
    Match,   // Vừa sức
    Safety   // An toàn
}

// Nhóm và lý do (mã), để bảng trường nói được vì sao trường nằm ở nhóm đó.
public record GroupDecision(SchoolGroup Group, IReadOnlyList<string> Reasons);

public static class GroupReasons
{
    public const string LowAcceptRate = "LOW_ACCEPT_RATE";        // tỉ lệ nhận dưới 15%: luôn Khó vào
    public const string BelowRange = "P_BELOW_RANGE";             // p' dưới 0,25
    public const string InRange = "P_IN_RANGE";                   // p' từ 0,25 đến dưới 0,75
    public const string AboveRange = "P_ABOVE_RANGE";             // p' từ 0,75 trở lên
    public const string CappedSelective = "CAPPED_SELECTIVE";     // lẽ ra An toàn nhưng tỉ lệ nhận dưới 50%
    public const string CappedWeakMetric = "CAPPED_WEAK_METRIC";  // lẽ ra An toàn nhưng một chỉ số dưới 0,25
    public const string IntlDowngrade = "INTL_DOWNGRADE";         // tỉ lệ nhận du học sinh thấp hơn hẳn: hạ một nhóm
    public const string NoAcceptRate = "NO_ACCEPT_RATE";          // thiếu tỉ lệ nhận nên bỏ luật theo tỉ lệ nhận
}

public static class SchoolGrouper
{
    // Xếp nhóm từ p' (đã hiệu chỉnh ngoại khóa), hai chỉ số gốc pg, ps và tỉ lệ nhận (dạng 0–1).
    // pg, ps, acceptRate, intlAcceptRate có thể thiếu (null).
    public static GroupDecision Classify(
        double pAdjusted, double? pg, double? ps, double? acceptRate, double? intlAcceptRate,
        GroupingOptions? options = null)
    {
        var o = options ?? new GroupingOptions();
        var reasons = new List<string>();
        if (acceptRate is null) reasons.Add(GroupReasons.NoAcceptRate);

        SchoolGroup group;
        if (acceptRate < o.AlwaysReachRate)
        {
            // Trường chọn lọc rất cao: điểm tốt đến đâu vẫn không chắc, không bao giờ coi là Vừa sức hay An toàn.
            group = SchoolGroup.Reach;
            reasons.Add(GroupReasons.LowAcceptRate);
        }
        else if (pAdjusted < o.LowerCut)
        {
            group = SchoolGroup.Reach;
            reasons.Add(GroupReasons.BelowRange);
        }
        else if (pAdjusted < o.UpperCut)
        {
            group = SchoolGroup.Match;
            reasons.Add(GroupReasons.InRange);
        }
        else
        {
            reasons.Add(GroupReasons.AboveRange);
            // p' đủ cao cho An toàn, nhưng trường nhận ít hơn một nửa hồ sơ, hoặc có một chỉ số dưới mốc
            // (chỉ số mạnh không được che chỉ số yếu) thì chỉ Vừa sức.
            if (acceptRate < o.SafetyMinRate)
            {
                group = SchoolGroup.Match;
                reasons.Add(GroupReasons.CappedSelective);
            }
            else if (pg < o.LowerCut || ps < o.LowerCut)
            {
                group = SchoolGroup.Match;
                reasons.Add(GroupReasons.CappedWeakMetric);
            }
            else
            {
                group = SchoolGroup.Safety;
            }
        }

        // Trường nhận du học sinh ít hơn hẳn tỉ lệ chung thì khó hơn con số trung bình: hạ một nhóm.
        if (acceptRate is double rate && rate > 0 && intlAcceptRate is double intl
            && intl / rate < o.IntlRatioCut && group > SchoolGroup.Reach)
        {
            group--;
            reasons.Add(GroupReasons.IntlDowngrade);
        }
        return new GroupDecision(group, reasons);
    }
}
