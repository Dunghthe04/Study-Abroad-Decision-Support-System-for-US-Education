using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>B6: luật chọn trung tâm cho chat gợi ý (CenterReferral) và các hàm của StudyCenterService dùng luật đó.</summary>
public class CenterReferralTests
{
    private static StudyCenter Center(
        string name,
        string? city = "Hà Nội",
        string[]? services = null,
        string[]? levels = null,
        string status = "verified",
        bool active = true) =>
        new()
        {
            Code = name.ToUpperInvariant(),
            Name = name,
            City = city,
            Services = [.. services ?? ["LUYEN_PHONG_VAN"]],
            StudyLevels = [.. levels ?? ["undergraduate"]],
            VerificationStatus = status,
            IsActive = active,
        };

    private static string[] Names(IEnumerable<StudyCenter> centers) => centers.Select(c => c.Name).ToArray();

    [Fact]
    public void Pick_KeepsOnlyActiveVerifiedCentersWithTheService()
    {
        var centers = new[]
        {
            Center("A"),
            Center("B", status: "unverified"),
            Center("C", active: false),
            Center("D", services: ["SUA_BAI_LUAN"]),
        };

        Assert.Equal(["A"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, null)));
    }

    [Fact]
    public void Pick_FiltersByStudyLevel_OnlyWhenGiven()
    {
        var centers = new[] { Center("A", levels: ["undergraduate"]), Center("B", levels: ["master"]) };

        Assert.Equal(["B"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", "master", null)));
        Assert.Equal(["A", "B"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, null)));
    }

    [Fact]
    public void Pick_PutsTheStudentsCityFirst_ThenSortsByName()
    {
        var centers = new[] { Center("A", city: "Hà Nội"), Center("B", city: "TP.HCM"), Center("C", city: null) };

        Assert.Equal(["B", "A", "C"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "TP.HCM")));
        Assert.Equal(["B", "A", "C"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "tp.hcm")));
    }

    [Fact]
    public void Pick_WithoutCity_SortsByNameOnly_SoNoCenterIsPushedUp()
    {
        var centers = new[] { Center("C", city: null), Center("A"), Center("B", city: "TP.HCM") };

        Assert.Equal(["A", "B", "C"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, null)));
    }

    [Fact]
    public void Pick_ReturnsAtMostThreeCenters()
    {
        var centers = new[] { Center("A"), Center("B"), Center("C"), Center("D") };

        Assert.Equal(CenterReferral.MaxCenters, CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, null).Count);
    }

    [Fact]
    public void Pick_ReturnsEmpty_WhenNoCenterMatches()
    {
        Assert.Empty(CenterReferral.Pick([Center("A")], "NOP_HO_SO", null, null));
    }

    [Fact]
    public void Cities_ListsDistinctCitiesOfReferableCentersOnly()
    {
        var centers = new[]
        {
            Center("A", city: "TP.HCM"),
            Center("B", city: "Hà Nội"),
            Center("C", city: "TP.HCM"),
            Center("D", city: "Đà Nẵng", status: "unverified"),
            Center("E", city: null),
        };

        Assert.Equal(["Hà Nội", "TP.HCM"], CenterReferral.Cities(centers));
    }

    private sealed class ReferableRepository(params StudyCenter[] centers) : IStudyCenterRepository
    {
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(centers);
    }

    [Fact]
    public async Task ReferAsync_AppliesTheRuleAndMapsToDtos()
    {
        var service = new StudyCenterService(new ReferableRepository(
            Center("A", city: "Hà Nội", services: ["LUYEN_PHONG_VAN", "CHON_TRUONG"]),
            Center("B", city: "TP.HCM")));

        var result = await service.ReferAsync(new CenterReferralQuery("LUYEN_PHONG_VAN", "undergraduate", "TP.HCM"));

        Assert.Equal(["B", "A"], result.Select(c => c.Name).ToArray());
        Assert.Contains("CHON_TRUONG", result[1].Services); // chat dùng dịch vụ, bậc học để AI giải thích vì sao hợp
    }

    [Fact]
    public async Task ReferAsync_RequiresAService()
    {
        var service = new StudyCenterService(new ReferableRepository());

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReferAsync(new CenterReferralQuery()));
    }

    [Fact]
    public async Task GetReferralCitiesAsync_ReturnsTheCitiesOfReferableCenters()
    {
        var service = new StudyCenterService(new ReferableRepository(Center("A", city: "TP.HCM"), Center("B")));

        Assert.Equal(["Hà Nội", "TP.HCM"], await service.GetReferralCitiesAsync());
    }
}
