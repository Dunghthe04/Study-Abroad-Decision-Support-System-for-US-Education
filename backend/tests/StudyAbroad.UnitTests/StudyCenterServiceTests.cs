using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class StudyCenterServiceTests
{
    private sealed class FakeRepository(params StudyCenter[] centers) : IStudyCenterRepository
    {
        public StudyCenterQuery? LastQuery { get; private set; }

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<StudyCenter>(centers, query.Page, query.PageSize, centers.Length));
        }

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(centers.FirstOrDefault(c => c.Id == id));
    }

    [Fact]
    public async Task SearchAsync_ClampsPagingValues()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo);

        await service.SearchAsync(new StudyCenterQuery(Page: 0, PageSize: 1000));

        Assert.Equal(1, repo.LastQuery!.Page);
        Assert.Equal(100, repo.LastQuery.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_MapsEntityToDto()
    {
        var center = new StudyCenter { Code = "CTY_001", Name = "Test", Services = ["CHON_TRUONG"] };
        var service = new StudyCenterService(new FakeRepository(center));

        var dto = await service.GetByIdAsync(center.Id);

        Assert.NotNull(dto);
        Assert.Equal("CTY_001", dto.Code);
        Assert.Contains("CHON_TRUONG", dto.Services);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        var service = new StudyCenterService(new FakeRepository());
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }
}
