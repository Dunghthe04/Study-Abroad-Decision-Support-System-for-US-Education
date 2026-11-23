using StudyAbroad.Application.Common;
using StudyAbroad.Application.Scholarships;
using Xunit;

namespace StudyAbroad.UnitTests;

public class ScholarshipSearchServiceTests
{
    private static ScholarshipDto Row(string? url) => new(
        Guid.NewGuid(), null, null, "Award", "Provider", "undergraduate", 1000m, "partial",
        null, null, null, null, url, null, "official");

    private class FakeRepo(params ScholarshipDto[] rows) : IScholarshipSearchRepository
    {
        public ScholarshipSearchQuery? Last;
        public Task<PagedResult<ScholarshipDto>> SearchAsync(ScholarshipSearchQuery query, CancellationToken ct = default)
        {
            Last = query;
            return Task.FromResult(new PagedResult<ScholarshipDto>(rows, query.Page, query.PageSize, rows.Length));
        }
    }

    [Fact]
    public async Task Search_ClampsPaging()
    {
        var repo = new FakeRepo();
        await new ScholarshipSearchService(repo).SearchAsync(new ScholarshipSearchQuery(Page: 0, PageSize: 1000));
        Assert.Equal(1, repo.Last!.Page);
        Assert.Equal(100, repo.Last.PageSize);
    }

    [Fact]
    public async Task Search_SwapsReversedAmountRange()
    {
        var repo = new FakeRepo();
        await new ScholarshipSearchService(repo).SearchAsync(new ScholarshipSearchQuery(MinAmountUsd: 9000, MaxAmountUsd: 1000));
        Assert.Equal(1000m, repo.Last!.MinAmountUsd);
        Assert.Equal(9000m, repo.Last.MaxAmountUsd);
    }

    [Fact]
    public async Task Search_DropsUnsafeApplyUrl()
    {
        var repo = new FakeRepo(Row(null) with { ApplyUrl = "http://localhost/apply" }, Row(null) with { ApplyUrl = "https://example.edu/apply" });
        var result = await new ScholarshipSearchService(repo).SearchAsync(new ScholarshipSearchQuery());
        Assert.Null(result.Items[0].ApplyUrl);
        Assert.Equal("https://example.edu/apply", result.Items[1].ApplyUrl);
    }

    [Fact]
    public async Task Search_DropsUnsafeSourceUrl()
    {
        var repo = new FakeRepo(Row("http://localhost/x"), Row("https://example.edu/aid"));
        var result = await new ScholarshipSearchService(repo).SearchAsync(new ScholarshipSearchQuery());
        Assert.Null(result.Items[0].SourceUrl);
        Assert.Equal("https://example.edu/aid", result.Items[1].SourceUrl);
    }
}
