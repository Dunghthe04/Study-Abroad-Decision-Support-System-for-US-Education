using System.Globalization;

namespace StudyAbroad.Application.Recommendations;

// Danh mục tham số của thuật toán gợi ý trường (UC-89, chỉ xem).
// Chiều dữ liệu: hằng số và giá trị mặc định trong code → danh mục → API → trang xem. Thuật toán KHÔNG đọc từ đây.
// Mỗi giá trị lấy trực tiếp từ code đang chạy, không chép tay, nên danh mục không thể lệch với thuật toán.
// Đổi một tham số trong code thì phải tăng RecommendationService.AlgorithmVersion (D6, BR-10).

// Key: mã ổn định để giao diện tham chiếu. Step: bước của thuật toán (B4, B9...). Value: chuỗi để hiển thị.
// Mọi tham số trong danh mục đều đã được thầy chốt, nên không có trạng thái riêng cho từng dòng.
public record AlgorithmParameter(string Key, string Step, string Name, string Value, string Meaning);

public static class AlgorithmParameters
{
    public static string Version => RecommendationService.AlgorithmVersion;

    public static IReadOnlyList<AlgorithmParameter> All { get; } = Build();

    // Chỉ nhận double hoặc decimal. Số nguyên thì dùng nội suy chuỗi hoặc ToString(): truyền int vào N sẽ lỗi mơ hồ giữa hai bản.
    private static string N(double v) => v.ToString(CultureInfo.InvariantCulture);
    private static string N(decimal v) => v.ToString(CultureInfo.InvariantCulture);
    private static string Pct(double v) => $"{N(Math.Round(v * 100, 2))}%";
    private static string Join(params string[] parts) => string.Join(" · ", parts);

    private static List<AlgorithmParameter> Build()
    {
        var g = new GroupingOptions();
        var english = new EnglishOptions();
        var goal = new GoalOptions();
        var fit = new FitWeights();
        var list = new List<AlgorithmParameter>();
        void Add(string key, string step, string name, string value, string meaning) =>
            list.Add(new AlgorithmParameter(key, step, name, value, meaning));

        // B4, B5: điều kiện nộp
        Add("budget_tolerance", "B5", "Dung sai ngân sách", Pct((double)RecommendationService.BudgetTolerance),
            "Trường có tổng chi phí một năm không vượt ngân sách quá mức này thì vẫn được giữ.");
        Add("english_points_per_band", "B4", "Điểm tương đương 1 band tiếng Anh",
            Join($"TOEFL {N(english.ToeflPointsPerBand)} điểm", $"Duolingo {N(english.DuolingoPointsPerBand)} điểm"),
            "Dùng để đổi mức thiếu của TOEFL, Duolingo ra band khi so với mức tối thiểu của trường.");
        Add("english_exclude", "B4", "Ngưỡng loại vì tiếng Anh", $"Thiếu từ {N(english.ExcludeFromShortfall)} band",
            "Thiếu từ mức này trở lên thì trường bị loại khỏi danh sách; thiếu ít hơn thì giữ kèm cảnh báo.");

        // B6: vị trí học thuật
        Add("gpa_sigma", "B6", "Mức dao động GPA giả định", N(AcademicPosition.DefaultGpaSigma),
            "Khi trường chỉ công bố GPA trung bình, hệ thống giả định GPA của sinh viên dao động quanh mức đó chừng này (độ lệch chuẩn) để ước tính vị trí của bạn.");
        Add("gpa_bins", "B6", "Các khoảng GPA của trường", string.Join("; ", AcademicPosition.GpaEdges.Select(x => N(x))),
            "Trường công bố tỉ lệ sinh viên theo bảy khoảng GPA. Các mốc này giúp biết bao nhiêu phần trăm sinh viên có GPA thấp hơn bạn.");
        Add("sat_min_p75", "B6", "Mốc SAT 75% tối thiểu của SAT tổng", SatScale.MinTotalP75.ToString(),
            "Mốc 75% từ mức này trở xuống bị coi là nhập sai thang và bỏ, không đoán.");

        // B7: gộp điểm
        Add("importance_weights", "B7", "Trọng số theo mức trường coi trọng",
            Join($"Rất quan trọng {N(AcademicBlend.Weight("very_important"))}", $"Quan trọng {N(AcademicBlend.Weight("important"))}",
                 $"Có xét {N(AcademicBlend.Weight("considered"))}", $"Không xét {N(AcademicBlend.Weight("not_considered"))}",
                 $"Chưa rõ {N(AcademicBlend.Weight(null))}"),
            "Trọng số của GPA và điểm thi khi gộp thành một điểm vị trí.");
        Add("low_submit", "B7", "Trường ít sinh viên nộp SAT",
            $"Dưới {Pct(g.LowSubmitPct)} nộp: trọng số điểm thi nhân {N(g.LowSubmitFactor)}",
            "Điểm SAT của trường chỉ đại diện nhóm điểm cao nộp bài, nên giảm trọng số.");

        // B8: ngoại khóa
        Add("ec_step", "B8", "Hiệu chỉnh theo ngoại khóa",
            $"{N(g.EcStep)} mỗi bậc lệch khỏi bậc {AcademicBlend.NeutralEcTier}, tối đa ±{N(g.EcCap)}",
            "Ngoại khóa chỉ nhích vị trí học thuật lên hoặc xuống trong giới hạn này.");
        Add("ec_importance", "B8", "Hệ số trường coi trọng ngoại khóa",
            Join($"Rất quan trọng {N(AcademicBlend.EcWeight("very_important"))}", $"Quan trọng {N(AcademicBlend.EcWeight("important"))}",
                 $"Có xét {N(AcademicBlend.EcWeight("considered"))}", $"Không xét {N(AcademicBlend.EcWeight("not_considered"))}"),
            "Nhân với mức hiệu chỉnh ngoại khóa.");

        // Bậc ngoại khóa
        Add("ec_activity_formula", "Bậc ngoại khóa", "Điểm một hoạt động",
            $"{N(ActivityTier.ScopeWeight)} × phạm vi/5 + {N(ActivityTier.RoleCoefficient)} × vai trò + {N(ActivityTier.DurationWeight)} × min(tháng/{N(ActivityTier.FullDurationMonths)}, 1)",
            "Điểm của từng hoạt động học sinh khai.");
        Add("ec_roles", "Bậc ngoại khóa", "Hệ số vai trò",
            Join($"Sáng lập {N(ActivityTier.RoleWeight("founder"))}", $"Trưởng {N(ActivityTier.RoleWeight("leader"))}",
                 $"Phó {N(ActivityTier.RoleWeight("vice_leader"))}", $"Thành viên {N(ActivityTier.RoleWeight(null))}"),
            "Vai trò trong hoạt động.");
        Add("ec_limits", "Bậc ngoại khóa", "Số mục được tính",
            Join($"{ActivityTier.TopActivities} hoạt động cao nhất", $"{ActivityTier.TopAwards} giải thưởng cao nhất",
                 $"{N(ActivityTier.AwardPoint)} điểm mỗi cấp giải", $"thưởng tối đa {N(ActivityTier.MaxBonus)}",
                 $"chỉ xét {ActivityTier.MaxActivities} mục mỗi loại"),
            "Giới hạn khi cộng điểm hoạt động và giải thưởng.");
        var cuts = ActivityTier.TierCuts;
        Add("ec_tier_cuts", "Bậc ngoại khóa", "Mốc chia bậc ngoại khóa",
            Join(cuts.Select((c, i) => $"dưới {N(c)}: bậc {i + 1}")
                .Append($"từ {N(cuts[^1])}: bậc {cuts.Length + 1}").ToArray()),
            $"Điểm gộp m = (tổng điểm hoạt động + thưởng giải) / {N(ActivityTier.MaxEcPoints)}, tối đa 1. Bậc 3 là trung bình, không làm đổi vị trí học thuật.");

        // B9: xếp nhóm
        Add("lower_cut", "B9", "Ngưỡng Khó vào / Vừa sức", N(g.LowerCut), "Vị trí học thuật dưới mức này là Khó vào.");
        Add("upper_cut", "B9", "Ngưỡng Vừa sức / An toàn", N(g.UpperCut), "Vị trí học thuật từ mức này là An toàn, nếu qua các luật chặn.");
        Add("always_reach_rate", "B9", "Tỉ lệ nhận luôn Khó vào", Pct(g.AlwaysReachRate),
            "Trường nhận dưới mức này luôn là Khó vào. Dùng tỉ lệ nhận của du học sinh nếu trường có, không thì tỉ lệ chung.");
        Add("safety_min_rate", "B9", "Tỉ lệ nhận tối thiểu để An toàn", Pct(g.SafetyMinRate),
            "Trường nhận dưới mức này không được xếp An toàn. Dùng tỉ lệ nhận của du học sinh nếu trường có, không thì tỉ lệ chung.");

        // B10, B11: điểm phù hợp và chọn trường
        Add("fit_weights", "B10", "Trọng số điểm phù hợp",
            // Thứ tự theo đặc tả UC-24: vừa tầm, ngân sách, chất lượng, học bổng, tiếng Anh
            Join($"Vừa tầm {N(fit.Position)}", $"Ngân sách {N(fit.Budget)}", $"Chất lượng {N(fit.Quality)}",
                 $"Học bổng {N(fit.Aid)}", $"Tiếng Anh {N(fit.English)}"),
            "Dùng để sắp thứ tự các trường trong cùng một nhóm; thiếu thành phần nào thì chia lại trọng số.");
        Add("max_per_block", "B11", "Số trường mỗi nhóm", RecommendationService.MaxPerBlock.ToString(),
            "Mỗi nhóm giữ tối đa chừng này trường.");

        // "Cần gì để vào" (goal-seeking)
        Add("goal_grid", "Cần gì để vào", "Lưới tìm kiếm",
            Join($"SAT mỗi {goal.SatStep} điểm ({goal.SatFloor} đến {goal.SatCeiling})",
                 $"GPA mỗi {N(goal.GpaStep)} ({N(goal.GpaFloor)} đến {N(goal.GpaCeiling)})", $"ngoại khóa đến bậc {goal.EcTierCeiling}"),
            "Độ mịn của phép thử khi tìm mức tối thiểu cần đạt.");
        Add("goal_effort", "Cần gì để vào", "Thang công sức",
            Join($"SAT +{N(goal.SatPerEffort)}", $"GPA +{N(goal.GpaPerEffort)}", $"ngoại khóa +{N(goal.EcPerEffort)} bậc", "mỗi mức là 1 đơn vị"),
            "Quy ước để so sánh công sức giữa các cách cải thiện.");
        Add("goal_levels", "Cần gì để vào", "Mức độ khó",
            $"Khả thi đến {N(goal.EasyUpTo)}; khó đến {N(goal.HardUpTo)}; trên đó rất khó",
            "Phân loại phương án theo tổng công sức.");
        Add("goal_baseline", "Cần gì để vào", "Mốc giả định khi thiếu điểm", $"SAT {goal.SatBaseline}; GPA {N(goal.GpaBaseline)}",
            "Chỉ dùng để so công sức khi học sinh chưa có điểm.");
        Add("goal_margin", "Cần gì để vào", "Đệm khuyến nghị", N(RecommendationService.GoalMargin),
            "Mức nên đặt mục tiêu cao hơn ngưỡng nhóm thêm chừng này vì số liệu có sai số.");
        Add("goal_alternatives", "Cần gì để vào", "Số phương án thay thế", RecommendationService.MaxAlternatives.ToString(),
            "Số trường thay thế gợi ý kèm.");

        return list;
    }
}
