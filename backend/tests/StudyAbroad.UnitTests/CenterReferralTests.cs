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
    public void Pick_WithACity_ReturnsOnlyCentersInThatCity_WithoutFillingFromOtherCities()
    {
        var centers = new[] { Center("A", city: "Hà Nội"), Center("B", city: "TP.HCM"), Center("C", city: null) };

        Assert.Equal(["B"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "TP.HCM")));
        Assert.Equal(["B"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "tp.hcm")));
    }

    [Fact]
    public void Pick_WithACityThatHasNoCenter_ReturnsEmpty()
    {
        var centers = new[] { Center("A", city: "Hà Nội"), Center("B", city: "TP.HCM") };

        Assert.Empty(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "Đà Nẵng"));
    }

    [Fact]
    public void Pick_WithACity_ReturnsAllCentersOfThatCity_SortedByName()
    {
        var centers = new[] { Center("D"), Center("B"), Center("A"), Center("C"), Center("X", city: "TP.HCM") };

        Assert.Equal(["A", "B", "C", "D"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, "Hà Nội")));
    }

    [Fact]
    public void CityCounts_CountsOnlyReferableCentersWithTheServiceAndLevel_MostCentersFirst()
    {
        var centers = new[]
        {
            Center("A", city: "Hà Nội"),
            Center("B", city: "Hà Nội"),
            Center("C", city: "TP.HCM"),
            Center("D", city: "Hải Phòng", services: ["SUA_BAI_LUAN"]),
            Center("E", city: "Đà Nẵng", status: "unverified"),
            Center("F", city: "Cần Thơ", levels: ["master"]),
            Center("G", city: null),
        };

        var counts = CenterReferral.CityCounts(centers, "LUYEN_PHONG_VAN", "undergraduate");

        Assert.Equal([("Hà Nội", 2), ("TP.HCM", 1)], counts.Select(c => (c.City, c.Count)).ToArray());
    }

    [Fact]
    public void CityCounts_IsEmpty_WhenNoCenterHasTheService()
    {
        Assert.Empty(CenterReferral.CityCounts([Center("A")], "NOP_HO_SO", null));
    }

    [Fact]
    public void Pick_WithoutCity_SortsByNameOnly_SoNoCenterIsPushedUp()
    {
        var centers = new[] { Center("C", city: null), Center("A"), Center("B", city: "TP.HCM") };

        Assert.Equal(["A", "B", "C"], Names(CenterReferral.Pick(centers, "LUYEN_PHONG_VAN", null, null)));
    }

    [Fact]
    public void Pick_ReturnsEveryMatchingCenter_UpToTheMaximum()
    {
        var few = new[] { Center("A"), Center("B"), Center("C"), Center("D"), Center("E"), Center("F"), Center("G") };
        var many = Enumerable.Range(0, CenterReferral.MaxCenters + 5).Select(i => Center($"C{i:00}")).ToArray();

        Assert.Equal(7, CenterReferral.Pick(few, "LUYEN_PHONG_VAN", null, null).Count); // 7 trung tâm khớp thì trả 7, không cắt còn 3
        Assert.Equal(CenterReferral.MaxCenters, CenterReferral.Pick(many, "LUYEN_PHONG_VAN", null, null).Count);
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

        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(centers);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(centers.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(StudyCenter center, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) =>
            Task.FromResult(centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId || (email != null && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase))));
    }

    private readonly FakeAuditLogRepository _auditLogs = new();

    [Fact]
    public async Task ReferAsync_AppliesTheRuleAndMapsToDtos()
    {
        var service = new StudyCenterService(new ReferableRepository(
            Center("A", city: "Hà Nội", services: ["LUYEN_PHONG_VAN", "CHON_TRUONG"]),
            Center("B", city: "TP.HCM")), _auditLogs);

        var result = await service.ReferAsync(new CenterReferralQuery("LUYEN_PHONG_VAN", "undergraduate", "Hà Nội"));

        Assert.Equal(["A"], result.Select(c => c.Name).ToArray()); // chỉ trung tâm ở Hà Nội, không lấp thêm B của TP.HCM
        Assert.Contains("CHON_TRUONG", result[0].Services); // chat dùng dịch vụ, bậc học để AI giải thích vì sao hợp
    }

    [Fact]
    public async Task GetReferralCityCountsAsync_CountsCentersPerCityForTheService()
    {
        var service = new StudyCenterService(new ReferableRepository(
            Center("A", city: "Hà Nội"), Center("B", city: "Hà Nội"), Center("C", city: "TP.HCM")), _auditLogs);

        var counts = await service.GetReferralCityCountsAsync(new CenterReferralQuery("LUYEN_PHONG_VAN", "undergraduate"));

        Assert.Equal([("Hà Nội", 2), ("TP.HCM", 1)], counts.Select(c => (c.City, c.Count)).ToArray());
    }

    [Fact]
    public async Task GetReferralCityCountsAsync_RequiresAService()
    {
        var service = new StudyCenterService(new ReferableRepository(), _auditLogs);

        await Assert.ThrowsAsync<ArgumentException>(() => service.GetReferralCityCountsAsync(new CenterReferralQuery()));
    }

    [Fact]
    public async Task ReferAsync_RequiresAService()
    {
        var service = new StudyCenterService(new ReferableRepository(), _auditLogs);

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReferAsync(new CenterReferralQuery()));
    }

    [Fact]
    public async Task GetReferralCitiesAsync_ReturnsTheCitiesOfReferableCenters()
    {
        var service = new StudyCenterService(new ReferableRepository(Center("A", city: "TP.HCM"), Center("B")), _auditLogs);

        Assert.Equal(["Hà Nội", "TP.HCM"], await service.GetReferralCitiesAsync());
    }
}
