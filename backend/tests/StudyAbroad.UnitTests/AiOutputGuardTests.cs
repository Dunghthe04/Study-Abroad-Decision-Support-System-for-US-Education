using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class AiOutputGuardTests
{
    private static readonly StudentSnapshot Student =
        new("undergraduate", "Computer Science", 3.5m, 1300, 60000, [], 8m, Ielts: 6.5m);

    // Oregon State: số liệu thật từ file của Dương → Match
    private static readonly SchoolCandidate Osu = new(Guid.NewGuid(), Guid.NewGuid(), "209542", "Oregon State University", "OR",
        ["Computer Science"], 3.70m, 1140, 1400, 38190, 16386, 2592, AcceptanceRate: 0.773m, MinIelts: 6m);

    private static ScoredSchool Scored(SchoolCandidate c) => RecommendationScorer.Score(Student, c, new RecommendSettings());

    // 3 trường đã chấm theo thứ tự CRM: Reach, Match, Safety
    private static readonly ScoredSchool Reach = Scored(Osu with { Code = "R", Name = "Reach U", AvgGpa4 = 3.9m });
    private static readonly ScoredSchool Match = Scored(Osu);
    private static readonly ScoredSchool Safety = Scored(Osu with { Code = "S", Name = "Safety U", AvgGpa4 = 3.1m, Sat25 = 1000, Sat75 = 1200 });
    private static readonly ScoredSchool[] Crm = [Reach, Match, Safety];

    private const string GoodReason = "GPA 3.5 so với GPA trung bình 3.70; SAT 1300 nằm giữa 1140 và 1400. Chi phí 57,168 USD/năm, ngân sách 60,000 USD.";

    private static string[] Codes(IEnumerable<GuardedPick> picks) => picks.Select(p => p.School.Candidate.Code).ToArray();

    [Fact]
    public void Crm_HasExpectedCategories()   // kiểm tra dữ liệu test đúng như mong muốn
    {
        Assert.Equal(AdmissionCategory.Reach, Reach.AdmissionCategory);
        Assert.Equal(AdmissionCategory.Match, Match.AdmissionCategory);
        Assert.Equal(AdmissionCategory.Safety, Safety.AdmissionCategory);
    }

    // ---------- Mã trường ----------

    [Fact]
    public void Apply_UnknownCode_IsDropped()
    {
        var r = AiOutputGuard.Apply([new("KHONG_CO", GoodReason), new("209542", GoodReason)], Crm, Student);
        Assert.DoesNotContain("KHONG_CO", Codes(r));
        Assert.Equal(3, r.Count);                                    // vẫn đủ 3 trường của CRM
    }

    [Fact]
    public void Apply_DuplicateCode_KeptOnce() =>
        Assert.Single(AiOutputGuard.Apply([new("209542", GoodReason), new("209542", GoodReason)], Crm, Student),
            p => p.School.Candidate.Code == "209542");

    [Fact]
    public void Apply_SchoolsAiOmitted_AreAppendedWithTemplate()
    {
        var r = AiOutputGuard.Apply([new("209542", GoodReason)], Crm, Student);
        Assert.Equal(["R", "209542", "S"], Codes(r));
        Assert.False(r.Single(p => p.School.Candidate.Code == "S").AiExplained);
    }

    [Fact]
    public void Apply_EmptyAiAnswer_ReturnsCrmListWithTemplates()
    {
        var r = AiOutputGuard.Apply([], Crm, Student);
        Assert.Equal(["R", "209542", "S"], Codes(r));
        Assert.All(r, p => Assert.False(p.AiExplained));
    }

    // ---------- Thứ tự và nhóm ----------

    [Fact]
    public void Apply_AiOrder_CannotMoveSchoolsAcrossCategories()
    {
        // LLM đưa Safety lên đầu → vẫn giữ Reach → Match → Safety
        var r = AiOutputGuard.Apply([new("S", "Trường an toàn."), new("209542", GoodReason), new("R", "Trường thử thách.")], Crm, Student);
        Assert.Equal(["R", "209542", "S"], Codes(r));
    }

    [Fact]
    public void Apply_AiOrder_IgnoredWithinCategory()
    {
        var match2 = Scored(Osu with { Code = "M2", Name = "Match Two" });
        ScoredSchool[] crm = [Match, match2];                        // SAW: 209542 trước M2

        var r = AiOutputGuard.Apply([new("M2", "Phù hợp."), new("209542", GoodReason)], crm, Student);
        Assert.Equal(["209542", "M2"], Codes(r));                    // LLM đưa M2 lên trước nhưng vẫn giữ thứ tự SAW
        Assert.All(r, p => Assert.True(p.AiExplained));               // lời giải thích AI vẫn được dùng
    }

    // ---------- Con số trong lời giải thích ----------

    [Theory]
    [InlineData(GoodReason)]
    [InlineData("GPA 3,5 so với 3,7; chi phí 57.168 USD.")]          // kiểu viết số của Việt Nam
    [InlineData("SAT 1,300 nằm trong khoảng 1,140–1,400.")]
    [InlineData("IELTS 6.5 đạt yêu cầu 6.0 của trường.")]
    [InlineData("SAT của bạn nằm giữa mốc 25% và 75%.")]             // mốc phân vị được phép
    [InlineData("Ngoại khóa 8/10, GPA 3.5/4.")]                      // thang điểm được phép
    [InlineData("Oregon State University có ngành Computer Science phù hợp.")]  // không có số
    public void IsGrounded_NumbersFromData_Accepted(string reason) =>
        Assert.True(AiOutputGuard.IsGrounded(reason, Student, Match));

    [Theory]
    [InlineData("GPA trung bình của trường là 3.8.")]                 // số bịa
    [InlineData("Chi phí khoảng 55,000 USD/năm.")]                   // số bịa
    [InlineData("Tổng chi phí 80,000 USD.")]
    [InlineData("Bạn có 70% cơ hội trúng tuyển.")]                   // phần trăm đậu
    [InlineData("Tỷ lệ nhận 77.3%.")]                                // tỷ lệ nhận không hiển thị
    [InlineData("Trường đứng top 50 nước Mỹ.")]                      // số không có trong dữ liệu
    [InlineData("")]
    [InlineData("   ")]
    public void IsGrounded_InventedNumbersOrChance_Rejected(string reason) =>
        Assert.False(AiOutputGuard.IsGrounded(reason, Student, Match));

    [Fact]
    public void IsGrounded_NumberOfAnotherSchool_Rejected()
    {
        // 3.9 là GPA trung bình của trường Reach, không phải của Oregon State
        Assert.True(AiOutputGuard.IsGrounded("GPA trung bình 3.9.", Student, Reach));
        Assert.False(AiOutputGuard.IsGrounded("GPA trung bình 3.9.", Student, Match));
    }

    [Fact]
    public void IsGrounded_TemplateReasons_AlwaysAccepted()   // giải thích soạn sẵn phải luôn qua được bộ kiểm tra
    {
        var belowEnglish = Scored(Osu with { MinIelts = 7m });     // có thêm câu "chưa đạt IELTS ≥ 7.0"
        foreach (var school in Crm.Append(belowEnglish))
            Assert.True(AiOutputGuard.IsGrounded(ReasonTemplate.Build(Student, school), Student, school),
                ReasonTemplate.Build(Student, school));
    }

    [Fact]
    public void IsGrounded_TooLong_Rejected() =>
        Assert.False(AiOutputGuard.IsGrounded(new string('a', 601), Student, Match));

    [Fact]
    public void Apply_InventedNumber_ReplacedByTemplate()
    {
        var r = AiOutputGuard.Apply([new("209542", "Chi phí chỉ 30,000 USD, rất rẻ.")], Crm, Student)
            .Single(p => p.School.Candidate.Code == "209542");

        Assert.False(r.AiExplained);
        Assert.Equal(ReasonTemplate.Build(Student, Match), r.Reason);
    }

    [Fact]
    public void Apply_GroundedReason_KeptAsAiExplained()
    {
        var r = AiOutputGuard.Apply([new("209542", "  " + GoodReason + " ")], Crm, Student)
            .Single(p => p.School.Candidate.Code == "209542");

        Assert.True(r.AiExplained);
        Assert.Equal(GoodReason, r.Reason);                          // bỏ khoảng trắng thừa
    }
}
