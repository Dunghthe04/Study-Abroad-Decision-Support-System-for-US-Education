using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StudyAbroad.Application.Dashboard.Roadmap;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>Lộ trình theo trường mục tiêu: tỉnh/thành học sinh và cách khớp với City của trung tâm.</summary>
public class RoadmapServiceTests
{
    private readonly Guid _studentId = Guid.NewGuid();

    [Fact]
    public void Provinces_Has34Entries()
    {
        Assert.Equal(34, VietnamProvinces.All.Count);
        Assert.Equal(34, new HashSet<string>(VietnamProvinces.All).Count);
    }

    [Theory]
    [InlineData("TP.HCM", "TP. Hồ Chí Minh")]
    [InlineData("tp hcm", "TP. Hồ Chí Minh")]
    [InlineData("Sài Gòn", "TP. Hồ Chí Minh")]
    [InlineData("Thành phố Hồ Chí Minh", "TP. Hồ Chí Minh")]
    [InlineData("ha noi", "Hà Nội")]
    [InlineData("TP. Hà Nội", "Hà Nội")]
    [InlineData("Tỉnh Đắk Lắk", "Đắk Lắk")]
    [InlineData("Thừa Thiên Huế", "Huế")]
    [InlineData("Đà Nẵng ", "Đà Nẵng")]
    [InlineData("Bình Dương", null)]
    [InlineData("", null)]
    public void Resolve_MapsFreeTextToCanonical(string input, string? expected)
    {
        Assert.Equal(expected, VietnamProvinces.Resolve(input));
    }

    [Fact]
    public void SameProvince_MatchesCenterCityAgainstStudentProvince()
    {
        Assert.True(VietnamProvinces.SameProvince("TP. Hồ Chí Minh", "TP.HCM"));
        Assert.True(VietnamProvinces.SameProvince("Hà Nội", "Ha Noi"));
        Assert.False(VietnamProvinces.SameProvince("Hà Nội", "Hải Phòng"));
        Assert.False(VietnamProvinces.SameProvince(null, null));
    }

    [Fact]
    public async Task SetProvince_SavesCanonicalName()
    {
        var repo = new FakeProfiles(new StudentProfile { UserId = _studentId });
        var service = new RoadmapService(repo, null!, null!, null!, null!, null!, null!);

        var result = await service.SetProvinceAsync(_studentId, "tp.hcm");

        Assert.Equal(SetProvinceResult.Saved, result);
        Assert.Equal("TP. Hồ Chí Minh", repo.Profile!.Province);
        Assert.Equal(1, repo.Updates);
    }

    [Fact]
    public async Task SetProvince_EmptyClearsValue()
    {
        var repo = new FakeProfiles(new StudentProfile { UserId = _studentId, Province = "Hà Nội" });
        var service = new RoadmapService(repo, null!, null!, null!, null!, null!, null!);

        var result = await service.SetProvinceAsync(_studentId, "  ");

        Assert.Equal(SetProvinceResult.Saved, result);
        Assert.Null(repo.Profile!.Province);
    }

    [Fact]
    public async Task SetProvince_RejectsUnknownProvince()
    {
        var repo = new FakeProfiles(new StudentProfile { UserId = _studentId });
        var service = new RoadmapService(repo, null!, null!, null!, null!, null!, null!);

        var result = await service.SetProvinceAsync(_studentId, "Paris");

        Assert.Equal(SetProvinceResult.InvalidProvince, result);
        Assert.Equal(0, repo.Updates);
    }

    [Fact]
    public async Task SetProvince_NoProfile_DoesNotCreateOne()
    {
        var repo = new FakeProfiles(null);
        var service = new RoadmapService(repo, null!, null!, null!, null!, null!, null!);

        var result = await service.SetProvinceAsync(_studentId, "Hà Nội");

        Assert.Equal(SetProvinceResult.NoProfile, result);
        Assert.Equal(0, repo.Updates);
    }

    [Fact]
    public async Task GetRoadmap_NoProfile_ReturnsNull()
    {
        var service = new RoadmapService(new FakeProfiles(null), null!, null!, null!, null!, null!, null!);

        Assert.Null(await service.GetRoadmapAsync(_studentId));
    }

    [Fact]
    public async Task GetChildRoadmap_WithoutActiveLink_IsNotLinked()
    {
        var parentId = Guid.NewGuid();
        var links = LinksStub.Create(new ParentStudentLink { ParentUserId = parentId, StudentUserId = _studentId, Status = ParentLinkStatuses.Pending });
        var service = new RoadmapService(new FakeProfiles(null), null!, null!, null!, null!, null!, links);

        var (linked, roadmap) = await service.GetChildRoadmapAsync(parentId, _studentId);

        Assert.False(linked);
        Assert.Null(roadmap);
    }

    [Fact]
    public async Task GetChildRoadmap_ActiveLink_ReturnsChildRoadmap()
    {
        var parentId = Guid.NewGuid();
        var links = LinksStub.Create(new ParentStudentLink { ParentUserId = parentId, StudentUserId = _studentId, Status = ParentLinkStatuses.Active });
        var service = new RoadmapService(new FakeProfiles(null), null!, null!, null!, null!, null!, links);

        var (linked, roadmap) = await service.GetChildRoadmapAsync(parentId, _studentId);

        Assert.True(linked);
        Assert.Null(roadmap); // con chưa có hồ sơ
    }

    // chỉ GetActiveByParentAsync được gọi; gọi hàm khác là lỗi test
    public class LinksStub : System.Reflection.DispatchProxy
    {
        private List<ParentStudentLink> _links = [];

        public static IParentLinkRepository Create(params ParentStudentLink[] links)
        {
            var proxy = Create<IParentLinkRepository, LinksStub>();
            ((LinksStub)(object)proxy)._links = [.. links];
            return proxy;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) =>
            method?.Name == nameof(IParentLinkRepository.GetActiveByParentAsync)
                ? Task.FromResult(_links.FindAll(l => l.ParentUserId == (Guid)args![0]!))
                : throw new NotSupportedException(method?.Name);
    }

    private sealed class FakeProfiles(StudentProfile? profile) : IProfileFlowRepository
    {
        public StudentProfile? Profile { get; } = profile;
        public int Updates { get; private set; }

        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Profile?.UserId == userId ? Profile : null);

        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
        {
            Updates++;
            return Task.CompletedTask;
        }

        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task CommitProfileFlowAsync(StudentProfile profile, IEnumerable<TranscriptScore> scores, IEnumerable<ProfileActivity> activities,
            IEnumerable<ProfileCertification> certifications, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
