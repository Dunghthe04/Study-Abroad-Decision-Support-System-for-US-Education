using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Infrastructure.Persistence;

namespace StudyAbroad.UnitTests;

/// <summary>
/// So seed metadata và ProfileConstants với file luật chung shared/profile_rules.json.
/// Advisor (Python) có test so với cùng file này (tests/test_profile_rules.py), sửa một bên quên bên kia thì đỏ.
/// </summary>
public class ProfileRulesTests
{
    private static readonly JsonElement Rules = LoadRules();
    private static readonly JsonElement Seed = LoadSeed();

    private static JsonElement LoadRules([CallerFilePath] string thisFile = "")
    {
        // Đi ngược từ file test này lên gốc repo để tìm shared/ (bin có thể nằm ngoài repo khi chạy dotnet test -o)
        var dir = new FileInfo(thisFile).Directory;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "shared", "profile_rules.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "shared", "profile_rules.json"))).RootElement;
    }

    private static JsonElement LoadSeed()
    {
        // Seed để private trong DbSeeder, đọc qua reflection để khỏi đổi code chạy thật
        var method = typeof(DbSeeder).GetMethod("GetDefaultProfileMetadataJson", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);
        return JsonDocument.Parse((string)method!.Invoke(null, null)!).RootElement;
    }

    private static List<string> Strings(JsonElement array) => array.EnumerateArray().Select(e => e.GetString()!).ToList();

    private static List<string> SeedIds(string key) => Seed.GetProperty(key).EnumerateArray().Select(e => e.GetProperty("id").GetString()!).ToList();

    [Theory]
    [InlineData("studyLevels")]
    [InlineData("currentEducationLevels")]
    [InlineData("scholarshipNeeds")]
    [InlineData("activityRoles")]
    [InlineData("impactLevels")]
    public void SeedIds_MatchSharedRules(string key) =>
        Assert.Equal(Strings(Rules.GetProperty(key)), SeedIds(key));

    [Fact]
    public void StudyLevels_AreValid()
    {
        foreach (var level in Strings(Rules.GetProperty("studyLevels")))
            Assert.True(StudyLevels.IsValid(level), level);
    }

    [Fact]
    public void ProfileConstants_MatchSharedRules()
    {
        Assert.Equal(Strings(Rules.GetProperty("scholarshipNeeds")).ToHashSet(), ProfileConstants.ScholarshipNeeds.All);
        Assert.Equal(Strings(Rules.GetProperty("activityRoles")).ToHashSet(), ProfileConstants.ActivityRoles.All);
        Assert.Equal(Strings(Rules.GetProperty("gpaModes")).ToHashSet(), ProfileConstants.GpaModes.All);
        Assert.Equal(ProfileConstants.ValidationLimits.MaxAnnualBudget, Rules.GetProperty("maxAnnualBudgetUsd").GetDecimal());
    }

    [Fact]
    public void ImpactLevels_KeepOrderOneToFive()
    {
        var codes = Strings(Rules.GetProperty("impactLevels"));
        Assert.Equal(codes.Count, ProfileConstants.ImpactLevelCodes.ToLevel.Count);
        for (var i = 0; i < codes.Count; i++)
            Assert.Equal(i + 1, ProfileConstants.ImpactLevelCodes.ToLevel[codes[i]]);
    }

    [Fact]
    public void BudgetTiers_MatchSeed()
    {
        var shared = Rules.GetProperty("budgetTiers").EnumerateArray().ToList();
        var seed = Seed.GetProperty("budgetTiers").EnumerateArray().ToList();
        Assert.Equal(shared.Count, seed.Count);
        decimal previousMax = 0;
        for (var i = 0; i < shared.Count; i++)
        {
            Assert.Equal(shared[i].GetProperty("id").GetString(), seed[i].GetProperty("id").GetString());
            Assert.Equal(shared[i].GetProperty("maxUsd").ToString(), seed[i].GetProperty("maxUsd").ToString());
            // Các mức nối liền nhau: min của mức này là max của mức trước
            Assert.Equal(previousMax, seed[i].GetProperty("minUsd").GetDecimal());
            if (seed[i].GetProperty("maxUsd").ValueKind == JsonValueKind.Number)
                previousMax = seed[i].GetProperty("maxUsd").GetDecimal();
        }
    }

    [Fact]
    public void CertLimits_MatchSeed()
    {
        var seed = Seed.GetProperty("certifications").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
        foreach (var cert in Rules.GetProperty("certLimits").EnumerateObject())
        {
            Assert.True(seed.ContainsKey(cert.Name), cert.Name);
            foreach (var field in new[] { "min", "max", "step" })
                Assert.Equal(cert.Value.GetProperty(field).GetDecimal(), seed[cert.Name].GetProperty(field).GetDecimal());
        }
    }

    [Fact]
    public void EnglishCerts_AreEnglishInSeed()
    {
        var seed = Seed.GetProperty("certifications").EnumerateArray().ToDictionary(c => c.GetProperty("id").GetString()!);
        foreach (var cert in Strings(Rules.GetProperty("englishCerts")))
            Assert.Equal("english", seed[cert].GetProperty("category").GetString());
    }
}
