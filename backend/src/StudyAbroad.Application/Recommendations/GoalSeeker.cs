namespace StudyAbroad.Application.Recommendations;

// Goal-seeking (UC-81): học sinh chọn một trường và nhóm muốn đạt, tìm mức tối thiểu của SAT, GPA, bậc ngoại khóa để trường đạt nhóm đó.
// Chạy lại đúng SchoolGrouper.Grade trên lưới giá trị nên mọi luật xếp nhóm (tỉ lệ nhận, hạ nhóm quốc tế...) tự được tính,
// không viết luật thứ hai. Tính từ hồ sơ thật, giữ các yếu tố khác nguyên. Không gọi AI.

// ============================== KIỂU DỮ LIỆU ==============================

// Yếu tố điều chỉnh được. ACT, tiếng Anh, ngân sách không nằm ở đây: ACT chỉ thay SAT khi thiếu SAT, tiếng Anh không đổi nhóm.
public enum GoalFactor
{
    Sat,
    Gpa,
    EcTier
}

public enum GoalOutcome
{
    AlreadyReached,  // trường đã ở nhóm đích hoặc cao hơn
    Reachable,       // đạt được bằng cách tăng các yếu tố
    Blocked,         // điểm cao đến đâu cũng không đạt nhóm đích (tỉ lệ nhận thấp, hạ nhóm quốc tế...)
    NoData           // trường chưa đủ dữ liệu để xếp nhóm
}

public enum GoalDifficulty
{
    Easy,      // khả thi
    Hard,      // khó
    VeryHard   // rất khó
}

// Tham số của lưới tìm kiếm và thang công sức. Mọi con số chỉnh được, không ghi cứng trong hàm tính.
public record GoalOptions(
    int SatStep = 10,
    decimal GpaStep = 0.05m,
    int SatFloor = 400,
    int SatCeiling = 1600,
    decimal GpaFloor = 2.0m,
    decimal GpaCeiling = 4.0m,
    int EcTierCeiling = 5,
    int SatBaseline = 1000,           // học sinh chưa có điểm SAT: mốc tính công sức
    decimal GpaBaseline = 3.0m,       // học sinh chưa có GPA: mốc tính công sức
    double SatPerEffort = 100,        // SAT +100 tính là một đơn vị công sức
    double GpaPerEffort = 0.1,        // GPA +0,1 tính là một đơn vị công sức
    double EcPerEffort = 1,           // ngoại khóa +1 bậc tính là một đơn vị công sức
    double EasyUpTo = 1.0,            // công sức tới mức này: khả thi
    double HardUpTo = 2.5);           // tới mức này: khó, trên đó: rất khó

// Một thay đổi: yếu tố, giá trị hiện tại (null nếu hồ sơ chưa có) và giá trị cần đạt.
public record GoalChange(GoalFactor Factor, decimal? From, decimal To);

public record GoalPlan(IReadOnlyList<GoalChange> Changes, double Effort, GoalDifficulty Difficulty);

// Kết quả tìm ngược cho một trường.
// Alone: với mỗi yếu tố tự đạt được, mức tối thiểu của riêng yếu tố đó (các yếu tố khác giữ nguyên).
// Cheapest: phương án ít công sức nhất, có thể kết hợp nhiều yếu tố; null khi không đạt hoặc đã đạt.
// NoEffect: yếu tố không làm đổi kết quả ở trường này (vd trường không xét ngoại khóa).
// Reasons: mã lý do (GroupReasons). Blocked, NoData: vì sao không đạt; còn lại: vì sao hiện chưa ở nhóm đích.
public record GoalSeekResult(
    SchoolGroup Current,
    SchoolGroup Target,
    GoalOutcome Outcome,
    SchoolGroup Best,
    IReadOnlyList<GoalPlan> Alone,
    GoalPlan? Cheapest,
    IReadOnlyList<GoalFactor> NoEffect,
    IReadOnlyList<string> Reasons);

// ============================== TÌM NGƯỢC ==============================

public static class GoalSeeker
{
    public static GoalSeekResult Seek(
        StudentAcademics student, SchoolAcademics school, SchoolGroup target,
        GroupingOptions? grouping = null, GoalOptions? options = null)
    {
        if (target == SchoolGroup.NoData)
            throw new ArgumentException("Nhóm đích phải là Khó vào, Vừa sức hoặc An toàn.", nameof(target));
        var o = options ?? new GoalOptions();

        var current = SchoolGrouper.Grade(student, school, grouping);
        if (current.Group != SchoolGroup.NoData && current.Group >= target)
            return new GoalSeekResult(current.Group, target, GoalOutcome.AlreadyReached, current.Group, [], null, [], current.Reasons);

        var sats = SatCandidates(student.Sat, o);
        var gpas = GpaCandidates(student.Gpa4, o);
        var ecs = EcCandidates(student.EcTier, o);

        // Yếu tố nào không làm đổi kết quả thì khỏi quét, và báo cho học sinh biết
        var noEffect = new List<GoalFactor>();
        if (!Matters(student, school, grouping, GoalFactor.Sat, sats, gpas, ecs)) { noEffect.Add(GoalFactor.Sat); sats = Only(sats); }
        if (!Matters(student, school, grouping, GoalFactor.Gpa, sats, gpas, ecs)) { noEffect.Add(GoalFactor.Gpa); gpas = Only(gpas); }
        if (!Matters(student, school, grouping, GoalFactor.EcTier, sats, gpas, ecs)) { noEffect.Add(GoalFactor.EcTier); ecs = Only(ecs); }

        // Điểm cao nhất có thể đạt (mọi yếu tố còn quét ở mức cao nhất): không đạt nhóm đích thì không cách nào đạt
        var top = SchoolGrouper.Grade(student with { Sat = sats[^1], Gpa4 = gpas[^1], EcTier = ecs[^1] }, school, grouping);
        if (top.Group == SchoolGroup.NoData)
            return new GoalSeekResult(current.Group, target, GoalOutcome.NoData, SchoolGroup.NoData, [], null, noEffect, top.Reasons);
        if (top.Group < target)
            return new GoalSeekResult(current.Group, target, GoalOutcome.Blocked, top.Group, [], null, noEffect, top.Reasons);

        GoalPlan? cheapest = null;
        var cheapestChanges = 0;
        var alone = new Dictionary<GoalFactor, GoalPlan>();

        foreach (var sat in sats)
            foreach (var gpa in gpas)
                foreach (var ec in ecs)
                {
                    var grade = SchoolGrouper.Grade(student with { Sat = sat, Gpa4 = gpa, EcTier = ec }, school, grouping);
                    if (grade.Group == SchoolGroup.NoData || grade.Group < target)
                        continue;

                    var changes = ChangesOf(student, sat, gpa, ec);
                    var effort = Effort(changes, student, o);
                    // Ít công sức hơn thắng; bằng nhau thì ít yếu tố hơn; bằng nữa thì gặp trước (SAT, GPA, ngoại khóa tăng dần)
                    if (cheapest is null || effort < cheapest.Effort || (effort == cheapest.Effort && changes.Count < cheapestChanges))
                    {
                        cheapest = new GoalPlan(changes, effort, DifficultyOf(effort, o));
                        cheapestChanges = changes.Count;
                    }
                    if (changes.Count == 1 && !alone.ContainsKey(changes[0].Factor))
                        alone[changes[0].Factor] = new GoalPlan(changes, effort, DifficultyOf(effort, o));
                }

        var ordered = alone.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();
        return new GoalSeekResult(current.Group, target, GoalOutcome.Reachable, top.Group, ordered, cheapest, noEffect, current.Reasons);
    }

    // Nhãn theo công sức: tới EasyUpTo là khả thi, tới HardUpTo là khó, trên đó là rất khó.
    public static GoalDifficulty DifficultyOf(double effort, GoalOptions? options = null)
    {
        var o = options ?? new GoalOptions();
        return effort <= o.EasyUpTo ? GoalDifficulty.Easy : effort <= o.HardUpTo ? GoalDifficulty.Hard : GoalDifficulty.VeryHard;
    }

    // Công sức của một phương án: SAT +100, GPA +0,1, ngoại khóa +1 bậc đều tính là một đơn vị. Chưa có điểm thì tính từ mốc giả định.
    public static double Effort(IReadOnlyList<GoalChange> changes, StudentAcademics student, GoalOptions? options = null)
    {
        var o = options ?? new GoalOptions();
        double total = 0;
        foreach (var change in changes)
        {
            total += change.Factor switch
            {
                GoalFactor.Sat => Math.Max(0, (double)(change.To - (student.Sat ?? o.SatBaseline))) / o.SatPerEffort,
                GoalFactor.Gpa => Math.Max(0, (double)(change.To - (student.Gpa4 ?? o.GpaBaseline))) / o.GpaPerEffort,
                _ => Math.Max(0, (double)(change.To - student.EcTier)) / o.EcPerEffort,
            };
        }
        return Math.Round(total, 2);
    }

    // ---------------------------------------------------------------- lưới giá trị

    // Giá trị thử, tăng dần, bắt đầu bằng "giữ nguyên". Chưa có giá trị thì phần tử đầu là null và quét từ sàn.
    private static List<int?> SatCandidates(int? current, GoalOptions o)
    {
        var list = new List<int?> { current };
        if (current is int sat && sat >= o.SatCeiling)
            return list;
        for (var v = current is int s ? s + o.SatStep : o.SatFloor; v < o.SatCeiling; v += o.SatStep)
            list.Add(v);
        list.Add(o.SatCeiling);
        return list;
    }

    private static List<decimal?> GpaCandidates(decimal? current, GoalOptions o)
    {
        var list = new List<decimal?> { current };
        if (current is decimal gpa && gpa >= o.GpaCeiling)
            return list;
        for (var v = current is decimal g ? g + o.GpaStep : o.GpaFloor; v < o.GpaCeiling; v += o.GpaStep)
            list.Add(v);
        list.Add(o.GpaCeiling);
        return list;
    }

    private static List<int> EcCandidates(int current, GoalOptions o)
    {
        var list = new List<int> { current };
        for (var tier = current + 1; tier <= o.EcTierCeiling; tier++)
            list.Add(tier);
        return list;
    }

    private static List<T> Only<T>(List<T> candidates) => [candidates[0]];

    // Chỉ còn một giá trị để thử và đó là giá trị hiện tại: không còn gì để tăng
    private static bool AtCeiling(GoalFactor factor, List<int?> sats, List<decimal?> gpas, List<int> ecs) => factor switch
    {
        GoalFactor.Sat => sats.Count == 1,
        GoalFactor.Gpa => gpas.Count == 1,
        _ => ecs.Count == 1,
    };

    // Yếu tố có làm đổi kết quả không: so sánh khi các yếu tố khác ở mức hiện tại và khi ở mức cao nhất.
    // Cần cả hai vì p' bị kẹp trong [0, 1]: khi các yếu tố khác đã tối đa thì ngoại khóa có thể không còn thấy khác biệt.
    private static bool Matters(
        StudentAcademics student, SchoolAcademics school, GroupingOptions? grouping, GoalFactor factor,
        List<int?> sats, List<decimal?> gpas, List<int> ecs)
    {
        if (AtCeiling(factor, sats, gpas, ecs))
            return true;   // không có gì để thử, coi như còn tác dụng (không báo "không tác dụng")

        StudentAcademics Raise(StudentAcademics s) => factor switch
        {
            GoalFactor.Sat => s with { Sat = sats[^1] },
            GoalFactor.Gpa => s with { Gpa4 = gpas[^1] },
            _ => s with { EcTier = ecs[^1] },
        };

        static (SchoolGroup, double?, double?, double?) Key(SchoolGrade g) => (g.Group, g.Pg, g.Ps, g.PAdjusted);

        var others = student;
        var allMax = student with { Sat = sats[^1], Gpa4 = gpas[^1], EcTier = ecs[^1] };
        // Các yếu tố khác ở mức cao nhất, riêng yếu tố đang xét về mức hiện tại
        var allMaxButThis = factor switch
        {
            GoalFactor.Sat => allMax with { Sat = student.Sat },
            GoalFactor.Gpa => allMax with { Gpa4 = student.Gpa4 },
            _ => allMax with { EcTier = student.EcTier },
        };

        var atCurrent = Key(SchoolGrouper.Grade(others, school, grouping)) != Key(SchoolGrouper.Grade(Raise(others), school, grouping));
        var atMax = Key(SchoolGrouper.Grade(allMaxButThis, school, grouping)) != Key(SchoolGrouper.Grade(allMax, school, grouping));
        return atCurrent || atMax;
    }

    // Các thay đổi so với hồ sơ: chỉ yếu tố có giá trị khác hiện tại
    private static List<GoalChange> ChangesOf(StudentAcademics student, int? sat, decimal? gpa, int ec)
    {
        var changes = new List<GoalChange>();
        if (sat != student.Sat && sat is int s) changes.Add(new GoalChange(GoalFactor.Sat, student.Sat, s));
        if (gpa != student.Gpa4 && gpa is decimal g) changes.Add(new GoalChange(GoalFactor.Gpa, student.Gpa4, g));
        if (ec != student.EcTier) changes.Add(new GoalChange(GoalFactor.EcTier, student.EcTier, ec));
        return changes;
    }
}
