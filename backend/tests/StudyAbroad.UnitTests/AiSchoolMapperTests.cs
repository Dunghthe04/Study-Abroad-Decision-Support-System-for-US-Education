using System.Text.Json;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class AiSchoolMapperTests
{
    //Colgate, số thật CDS 2025–26 (ví dụ trong GOI_Y_TRUONG_TONG_QUAN và AI_CACH_THUC_HIEN mục 4).
    //Số rate để dạng numeric(5,4) như DB trả về (0.1750) để kiểm tra bỏ số 0 thừa.
    private static SchoolCandidate Colgate() => new(
        Guid.NewGuid(), Guid.NewGuid(), "190099", "Colgate University", "NY", [],
        AvgGpa4: 3.85m, Sat25: 1450, Sat75: 1510,
        TuitionUsd: 73206m, LivingUsd: 18446m, FeesUsd: 440m, OtherExpensesUsd: 3518m,   // = 95.610
        AcceptanceRate: 0.1750m, Control: "private", SatPolicy: "optional",
        C7GpaImportance: "very_important", C7TestImportance: "important", C7EcImportance: "important",
        SatSubmitPct: 0.2300m,
        GpaDistribution: new GpaDistribution(0.31m, 0.38m, 0.23m, 0.06m, 0.02m, 0m, 0m),
        IntlAcceptanceRate: 0.0440m, IntlAidAvgUsd: 86649m, RetentionRate1Yr: 0.92m, GradRate6Yr: 0.89m);

    private static string Json(SchoolCandidate s) =>
        JsonSerializer.Serialize(AiSchoolMapper.ToAi(s, out _), AiJson.Options);

    [Fact]
    public void Colgate_MatchesDocumentExactly()
    {
        // Đúng chuỗi trong mục 4 file AI_CACH_THUC_HIEN (và đúng compact_school bên Python)
        Assert.Equal(
            """{"c":"190099","acc":0.175,"iacc":0.044,"sat":[1450,1510],"subm":0.23,"satp":"opt","gpa":[0,0,0.02,0.06,0.23,0.38,0.31],"avg":3.85,"L":[3,2,2],"coa":95610,"aid":86649,"q":0.905}""",
            Json(Colgate()));
    }

    [Fact]
    public void GpaBands_AreSentLowToHigh()
    {
        var ai = AiSchoolMapper.ToAi(Colgate(), out _);
        Assert.Equal(0.31m, ai.GpaBands![6]);    // k7 = 4.0 đứng cuối
        Assert.Equal(0m, ai.GpaBands[0]);        // k1 = dưới 2.50 đứng đầu
    }

    [Fact]
    public void MissingValues_AreDroppedFromJson()
    {
        var bare = new SchoolCandidate(Guid.NewGuid(), Guid.NewGuid(), "X", "X", null, [], null, null, null, null, null, null,
            C7GpaImportance: "important", C7TestImportance: "important", C7EcImportance: "considered");
        Assert.Equal("""{"c":"X","L":[2,2,1]}""", Json(bare));
    }

    [Fact]
    public void MissingC7_PrivateSchool_UsesSelectiveDefaultAndFlags()
    {
        var ai = AiSchoolMapper.ToAi(Colgate() with { C7EcImportance = null }, out var flags);
        Assert.Equal([3, 2, 2], ai.Importance);
        Assert.Contains(AiSchoolMapper.C7DefaultFlag, flags);
    }

    [Fact]
    public void MissingC7_LessSelectivePublicSchool_UsesPublicDefault()
    {
        var school = Colgate() with { Control = "public", AcceptanceRate = 0.8m,
            C7GpaImportance = null, C7TestImportance = null, C7EcImportance = null };
        Assert.Equal([2, 2, 1], AiSchoolMapper.ToAi(school, out _).Importance);
    }

    [Fact]
    public void OnlyMissingC7Item_IsFilled_OthersKept()
    {
        var ai = AiSchoolMapper.ToAi(Colgate() with { C7TestImportance = null }, out _);
        Assert.Equal([3, 2, 2], ai.Importance);   // GPA 3 và ngoại khóa 2 giữ nguyên, SAT điền mặc định 2
    }

    [Fact]
    public void CompleteC7_HasNoFlag()
    {
        AiSchoolMapper.ToAi(Colgate(), out var flags);
        Assert.Empty(flags);
    }

    [Fact]
    public void IncompleteGpaDistribution_IsNotSent()
    {
        var school = Colgate() with { GpaDistribution = new GpaDistribution(0.31m, 0.38m, null, 0.06m, 0.02m, 0m, 0m) };
        Assert.Null(AiSchoolMapper.ToAi(school, out _).GpaBands);
    }

    [Fact]
    public void InvalidSatRange_IsNotSent()
    {
        Assert.Null(AiSchoolMapper.ToAi(Colgate() with { Sat25 = 1500, Sat75 = 1400 }, out _).Sat);
        Assert.Null(AiSchoolMapper.ToAi(Colgate() with { Sat75 = null }, out _).Sat);
    }

    [Fact]
    public void Quality_UsesAvailableRatesOnly()
    {
        Assert.Equal(0.92m, AiSchoolMapper.ToAi(Colgate() with { GradRate6Yr = null }, out _).Quality);
        Assert.Null(AiSchoolMapper.ToAi(Colgate() with { GradRate6Yr = null, RetentionRate1Yr = null }, out _).Quality);
    }

    [Theory]
    [InlineData("required", "req")]
    [InlineData("optional", "opt")]
    [InlineData("not_accepted", "no")]
    [InlineData(null, null)]
    public void SatPolicy_IsShortened(string? policy, string? expected) =>
        Assert.Equal(expected, AiSchoolMapper.ToAi(Colgate() with { SatPolicy = policy }, out _).SatPolicy);

    [Fact]
    public void TotalCost_IncludesOtherExpenses()
    {
        Assert.Equal(95610m, Colgate().TotalCostUsd);
        Assert.Equal(92092m, (Colgate() with { OtherExpensesUsd = null }).TotalCostUsd);
    }
}
