using System.Text.Json;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class SchoolScorerTests
{
    private static readonly AiStudent An = new(3.6m, 1350, 50000m, 3);

    // 3 trường trong GOI_Y_TRUONG_TONG_QUAN, đã chuẩn hóa như mục 4 file AI_CACH_THUC_HIEN
    private static readonly AiSchool Colgate = new("190099", 0.175m, 0.044m, [1450, 1510], 0.23m, "opt",
        [0m, 0m, 0.02m, 0.06m, 0.23m, 0.38m, 0.31m], 3.85m, [3, 2, 2], 95610m, 86649m, 0.905m);
    private static readonly AiSchool SchoolB = new("B", 0.60m, null, [1100, 1300], 1.0m, "req",
        [0.02m, 0.08m, 0.20m, 0.28m, 0.30m, 0.08m, 0.04m], null, [2, 2, 0], 38000m, 4000m, 0.80m);
    private static readonly AiSchool SchoolC = new("C", 0.45m, null, [1250, 1400], 1.0m, "req",
        [0.01m, 0.05m, 0.14m, 0.20m, 0.25m, 0.20m, 0.15m], null, [3, 2, 1], 46000m, 12000m, 0.90m);

    [Fact]
    public void An_AtColgate_MatchesDocument()
    {
        var r = SchoolScorer.Score(An, Colgate);
        Assert.Equal([0.75m, 0.25m], r.W);
        Assert.Equal((17.2m, "H", 0m), (r.Pg!.Value, r.Conf, r.Ps!.Value));
        Assert.Equal((12.9m, 0m, 12.9m), (r.A!.Value, r.Adj, r.S!.Value));
        Assert.Equal(("R", 0.25m, "RF"), (r.G, r.R!.Value, r.G2));       // Khó vào xa
        Assert.Equal((0m, 0.906m, 0.905m, 45610m), (r.F!.Value, r.H!.Value, r.Q!.Value, r.Gap!.Value));
    }

    [Fact]
    public void An_AtSchoolB_And_C_MatchDocument()
    {
        var b = SchoolScorer.Score(An, SchoolB);
        Assert.Equal((70m, 87.5m, 78.75m, "S", "S"), (b.Pg!.Value, b.Ps!.Value, b.S!.Value, b.G, b.G2));
        Assert.Equal((0.24m, 0.105m), (b.F!.Value, b.H!.Value));

        var c = SchoolScorer.Score(An, SchoolC);
        Assert.Equal([0.6m, 0.4m], c.W);
        Assert.Equal((50m, 58.33m, 53.33m, "M"), (c.Pg!.Value, c.Ps!.Value, c.S!.Value, c.G2));
        Assert.Null(c.R);                                                  // không có tỉ lệ du học sinh: không hạ nhóm
    }

    [Fact]
    public void Binh_AtColgate_IsDowngradedFromMatchToReach()
    {
        var r = SchoolScorer.Score(new AiStudent(3.9m, 1480, 50000m, 2), Colgate);
        Assert.Equal((50m, 53.8m, -2m, 50.85m), (r.Ps!.Value, r.Pg!.Value, r.Adj, r.S!.Value));
        Assert.Equal(("M", "R"), (r.G, r.G2));
    }

    [Fact]
    public void StudentWithoutSat_UsesGpaOnly()
    {
        var r = SchoolScorer.Score(An with { Sat = null }, Colgate);
        Assert.Equal([1m, 0m], r.W);
        Assert.Null(r.Ps);
    }

    [Fact]
    public void OnlyGpaAverage_UsesZTableAndLowConfidence()
    {
        var r = SchoolScorer.Score(An, Colgate with { GpaBands = null });
        Assert.Equal((21m, "L"), (r.Pg!.Value, r.Conf));                  // z = −0,83 → nội suy 16..31
    }

    [Theory]
    [InlineData(5, 4)]     // (5 − 3) × L_EC 2
    [InlineData(1, -4)]
    [InlineData(3, 0)]
    public void ExtracurricularAdjustment_IsTierMinusThreeTimesImportance(int tier, int adj) =>
        Assert.Equal(adj, SchoolScorer.Score(An with { Tier = tier }, Colgate).Adj);

    [Fact]
    public void NoGpaAndNoSatData_IsNotGrouped()
    {
        var bare = new AiSchool("X", 0.5m, null, null, null, null, null, null, [3, 2, 1], null, null, null);
        var r = SchoolScorer.Score(An, bare);
        Assert.Equal((null, "NA", "NA"), (r.S, r.G, r.G2));
    }

    [Fact]
    public void OpenAdmission_IsGroupO()
    {
        Assert.Equal("O", SchoolScorer.Score(An, SchoolB with { AcceptanceRate = 1.0m }).G2);
    }

    //Toàn bộ bộ đánh giá 216 cặp hồ sơ–trường: C# phải ra đúng đáp án của reference.py
    [Fact]
    public void MatchesReferenceOnAll216EvalPairs()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(CasesPath()));
        var checkedPairs = 0;
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var student = c.GetProperty("student").Deserialize<AiStudent>(AiJson.Options)!;
            var schools = c.GetProperty("schools").Deserialize<List<AiSchool>>(AiJson.Options)!;
            var expected = c.GetProperty("expected").Deserialize<List<AiScoreItem>>(AiJson.Options)!;
            foreach (var (school, want) in schools.Zip(expected))
            {
                var got = SchoolScorer.Score(student, school);
                var where = $"{c.GetProperty("id").GetString()} {school.Code}";
                Assert.True(want.G2 == got.G2, $"{where}: nhóm {got.G2} ≠ {want.G2}");
                Assert.True(Near(want.S, got.S, 0.01m), $"{where}: S {got.S} ≠ {want.S}");
                Assert.True(Near(want.Pg, got.Pg, 0.01m) && Near(want.Ps, got.Ps, 0.01m), $"{where}: điểm vị trí lệch");
                Assert.True(Near(want.W[0], got.W[0], 0.01m), $"{where}: trọng số {got.W[0]} ≠ {want.W[0]}");
                Assert.True(Near(want.F, got.F, 0.001m) && Near(want.H, got.H, 0.001m), $"{where}: F/H lệch");
                checkedPairs++;
            }
        }
        Assert.Equal(216, checkedPairs);
    }

    private static bool Near(decimal? want, decimal? got, decimal tolerance) =>
        want is null ? got is null : got is not null && Math.Abs(want.Value - got.Value) <= tolerance;

    //Tìm advisor/evals/recommendation/cases.json: đi ngược lên từ thư mục chạy test, rồi từ thư mục chứa file test này
    private static string CasesPath([System.Runtime.CompilerServices.CallerFilePath] string sourceFile = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(sourceFile) ?? "" })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                var path = Path.Combine(dir.FullName, "advisor", "evals", "recommendation", "cases.json");
                if (File.Exists(path)) return path;
            }
        }
        throw new FileNotFoundException("Không tìm thấy advisor/evals/recommendation/cases.json");
    }
}
