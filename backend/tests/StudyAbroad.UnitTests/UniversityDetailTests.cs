using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Infrastructure.Persistence;
using StudyAbroad.Infrastructure.Persistence.Repositories;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Universities;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class UniversityDetailTests
{
    [Fact]
    public async Task MissingCodeReturnsNull()
    {
        var repository = new FakeRepository(null);
        Assert.Null(await new UniversityDetailService(repository).GetByCodeAsync("UNKNOWN"));
        Assert.Equal("UNKNOWN", repository.Code);
    }

    [Fact]
    public async Task BlankCodeDoesNotCallRepository()
    {
        var repository = new FakeRepository(null);
        Assert.Null(await new UniversityDetailService(repository).GetByCodeAsync(" "));
        Assert.Null(repository.Code);
    }

    [Fact]
    public async Task InactiveSchoolHidesAllDetails()
    {
        var data = CreateData();
        data.University.IsActive = false;
        Assert.Null(await new UniversityDetailService(new FakeRepository(data)).GetByCodeAsync("TEST"));
    }

    [Fact]
    public async Task MapsExistingInformationWithoutAuditFields()
    {
        var data = CreateData();
        var offering = new UniversityOffering { UniversityId = data.University.Id, StudyLevel = "undergraduate", TuitionUsd = 45000, MinGpa4 = 4.2m };
        var repository = new FakeRepository(data with
        {
            Offerings = [offering],
            Majors = [new UniversityMajorRow(offering.Id, "Computer Science", "11.0701", true, false)],
            Media = [new Media { Type = "logo", Url = "https://example.edu/logo.png" }, new Media { Type = "gallery", Url = "file:///private/photo.jpg" }]
        });
        var result = await new UniversityDetailService(repository).GetByCodeAsync(" test ");
        Assert.NotNull(result);
        Assert.Equal("TEST", repository.Code);
        Assert.Equal("California", result.StateName);
        Assert.Equal("https://example.edu/logo.png", result.LogoUrl);
        Assert.Empty(result.GalleryUrls);
        var resultOffering = Assert.Single(result.Offerings);
        Assert.Equal(45000m, resultOffering.TuitionUsd);
        Assert.Equal(4.2m, resultOffering.MinGpa4);
        Assert.False(Assert.Single(resultOffering.Majors).IsVerified);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("LastEditedBy", json);
        Assert.DoesNotContain("CreatedAt", json);
        Assert.DoesNotContain(data.University.Id.ToString(), json);
        Assert.DoesNotContain("private-note", json);
    }

    [Fact]
    public void EvidenceOnlyExposesAllowlistedMetadata()
    {
        var evidence = new FieldEvidence
        {
            FieldName = "tuition_usd", FieldValue = "45000", SourceUrl = "https://example.edu/costs",
            Status = "unverified", VerifiedBy = "private-auditor",
            ExtractedQuote = """{"retrieved_at":"2026-10-26T00:00:00","period_year":2025,"normalization":"accepted","applied_to_snapshot":true,"workbook":"D:\\private.xlsx","quote":"private text","checked_by":"private-auditor"}"""
        };
        var result = UniversityDetailService.MapSource(evidence);
        Assert.Equal(new DateOnly(2026, 10, 26), result.RetrievedAt);
        Assert.Equal("2025", result.DataYear);
        Assert.Equal("unverified", result.Status);
        Assert.True(result.AppliedToSnapshot);
        Assert.Equal("accepted", result.Normalization);
        Assert.Equal("45000", result.FieldValue);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private", json);
        Assert.DoesNotContain("ExtractedQuote", json);
    }

    [Theory]
    [InlineData("not JSON")]
    [InlineData("{\"retrieved_at\": \"bad date\"}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void MalformedOrMissingMetadataDoesNotInventDates(string metadata)
    {
        var result = UniversityDetailService.MapSource(new FieldEvidence { ExtractedQuote = metadata, FieldValue = "5" });
        Assert.Null(result.RetrievedAt);
        Assert.Null(result.DataYear);
        Assert.Null(result.FieldValue);
    }

    [Fact]
    public void UnappliedVerifiedCandidateIsNotExposedAsCurrentValue()
    {
        var result = UniversityDetailService.MapSource(new FieldEvidence
        {
            Status = "verified", FieldValue = "99999",
            ExtractedQuote = """{"applied_to_snapshot":false,"normalization":"out_of_range"}"""
        });
        Assert.Equal("verified", result.Status);
        Assert.False(result.AppliedToSnapshot);
        Assert.Equal("out_of_range", result.Normalization);
        Assert.Null(result.FieldValue);
    }

    [Fact]
    public void LargeMetadataIsNotReturnedOrParsed()
    {
        var result = UniversityDetailService.MapSource(new FieldEvidence { ExtractedQuote = new string('x', 20000) });
        Assert.Null(result.RetrievedAt);
        Assert.True(JsonSerializer.Serialize(result).Length < 1000);
    }

    [Theory]
    [InlineData("file:///D:/private/source.pdf")]
    [InlineData("http://localhost/source")]
    [InlineData("http://127.0.0.1/source")]
    [InlineData("http://192.168.1.10/source")]
    [InlineData("https://user:password@example.edu/source")]
    [InlineData("javascript:alert(1)")]
    public void RejectsNonPublicLinks(string url) => Assert.Null(UniversityDetailService.PublicUrl(url));

    [Fact]
    public async Task SourceRowsAreDeduplicatedAndLimited()
    {
        var evidence = Enumerable.Range(0, 300).Select(i => new FieldEvidence
        {
            FieldName = "tuition_usd", SourceUrl = "https://example.edu/" + (i % 150), Status = "unverified"
        }).ToList();
        var result = await new UniversityDetailService(new FakeRepository(CreateData() with { Evidence = evidence })).GetByCodeAsync("TEST");
        Assert.NotNull(result);
        Assert.Equal(100, result.Sources.Count);
        Assert.Equal(100, result.Sources.Select(s => s.SourceUrl).Distinct().Count());
    }

    [Fact]
    public async Task CancellationIsForwarded()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new FakeRepository(CreateData());
        await new UniversityDetailService(repository).GetByCodeAsync("TEST", cancellation.Token);
        Assert.Equal(cancellation.Token, repository.Token);
    }

    [Fact]
    public async Task ControllerIsExplicitlyPublicAndReturns404()
    {
        Assert.NotNull(typeof(UniversitiesController).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal("api/v1/universities", typeof(UniversitiesController).GetCustomAttribute<RouteAttribute>()?.Template);
        var controller = new UniversitiesController(new UniversityDetailService(new FakeRepository(null)));
        Assert.IsType<NotFoundResult>((await controller.GetByCode("MISSING", default)).Result);
    }

    [Fact]
    public async Task ControllerReturnsKnownSchool()
    {
        var controller = new UniversitiesController(new UniversityDetailService(new FakeRepository(CreateData())));
        var response = await controller.GetByCode("TEST", default);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        Assert.Equal("TEST", Assert.IsType<UniversityDetailDto>(ok.Value).Code);
    }

    [Fact]
    public async Task ControllerSearchReturnsPagedResult()
    {
        var repo = new FakeRepository(CreateData());
        var controller = new UniversitiesController(new UniversityDetailService(repo));
        var query = new UniversitySearchQuery(Search: "Test", State: "CA", Page: 1, PageSize: 10);
        var response = await controller.Search(query, default);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var result = Assert.IsType<PagedResult<UniversityListItemDto>>(ok.Value);
        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("TEST", result.Items[0].Code);
        Assert.Equal(query, repo.LastQuery);
    }

    [Fact]
    public async Task ControllerFilterOptionsReturnsOptions()
    {
        var repo = new FakeRepository(CreateData());
        var controller = new UniversitiesController(new UniversityDetailService(repo));
        var response = await controller.GetFilterOptions(default);
        var ok = Assert.IsType<OkObjectResult>(response.Result);
        var options = Assert.IsType<UniversityFilterOptionsDto>(ok.Value);
        Assert.Single(options.States);
        Assert.Equal("CA", options.States[0].Value);
        Assert.Equal(2, options.Controls.Count);
        Assert.Equal(10000m, options.MinTuitionUsd);
    }

    [Fact]
    public async Task ServiceSearchForwardsQueryAndToken()
    {
        using var cancellation = new CancellationTokenSource();
        var repo = new FakeRepository(CreateData());
        var service = new UniversityDetailService(repo);
        var query = new UniversitySearchQuery(Search: "Stanford", IsIvy: true);
        var result = await service.SearchAsync(query, cancellation.Token);
        Assert.Equal(query, repo.LastQuery);
        Assert.Equal(cancellation.Token, repo.Token);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ServiceFilterOptionsForwardsToken()
    {
        using var cancellation = new CancellationTokenSource();
        var repo = new FakeRepository(CreateData());
        var service = new UniversityDetailService(repo);
        var result = await service.GetFilterOptionsAsync(cancellation.Token);
        Assert.Equal(cancellation.Token, repo.Token);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("QueryMajors")]
    [InlineData("QueryRankings")]
    public void RepositoryOrderedProjectionsTranslateToPostgres(string methodName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only;Username=unused;Password=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);
        var repository = new UniversityDetailRepository(db);
        var method = typeof(UniversityDetailRepository).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object argument = methodName == "QueryMajors" ? new[] { Guid.NewGuid() } : Guid.NewGuid();
        var query = Assert.IsAssignableFrom<IQueryable>(method.Invoke(repository, [argument]));
        var sql = query.ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("JOIN", sql);
    }

    private static UniversityDetailData CreateData() => new(
        new University { Code = "TEST", Name = "Test University", State = "CA", LastEditedByUserId = Guid.NewGuid() },
        "California", [], [], [], [], []);

    private sealed class FakeRepository(UniversityDetailData? data) : IUniversityDetailRepository
    {
        public string? Code { get; private set; }
        public CancellationToken Token { get; private set; }
        public UniversitySearchQuery? LastQuery { get; private set; }

        public Task<UniversityDetailData?> GetByCodeAsync(string code, CancellationToken ct = default)
        {
            Code = code;
            Token = ct;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(data);
        }

        public Task<PagedResult<UniversityListItemDto>> SearchAsync(UniversitySearchQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            Token = ct;
            ct.ThrowIfCancellationRequested();
            var items = new List<UniversityListItemDto>();
            if (data != null && data.University.IsActive)
            {
                items.Add(new UniversityListItemDto(
                    data.University.Id, data.University.Code, data.University.Name,
                    data.University.City, data.University.State, data.StateName,
                    data.University.Control, data.University.AcceptanceRate, data.University.IsIvy,
                    null, null, 45000, 60000, 6.5m, 80, 105, 10, "US News", "optional", "need_and_merit", []));
            }
            return Task.FromResult(new PagedResult<UniversityListItemDto>(items, query.Page, query.PageSize, items.Count));
        }

        public Task<UniversityFilterOptionsDto> GetFilterOptionsAsync(CancellationToken ct = default)
        {
            Token = ct;
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new UniversityFilterOptionsDto(
                [new("CA", "California (CA)")],
                [new("public", "Công lập (Public)"), new("private", "Tư thục (Private)")],
                ["Computer Science"],
                10000, 60000, 15000, 85000));
        }
    }
}

