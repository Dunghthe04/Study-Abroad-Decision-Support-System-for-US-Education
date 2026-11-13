using System.Text.Json;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

// UC-37: nguồn của câu trả lời kiến thức mang theo ngày thu thập, từ advisor tới trình duyệt.
public class CitationDateTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Citation_FromAdvisorJson_ReadsTheCollectedDate()
    {
        const string json = """{"title":"8 CFR 214.13","url":"https://example.org/sevis","snippet":"...","retrievedAt":"2026-10-05"}""";

        var citation = JsonSerializer.Deserialize<Citation>(json, Web)!;

        Assert.Equal(new DateOnly(2026, 10, 5), citation.RetrievedAt);
        Assert.Equal("https://example.org/sevis", citation.Url);
    }

    [Theory]
    [InlineData("""{"title":"Nguồn A"}""")]
    [InlineData("""{"title":"Nguồn A","retrievedAt":null}""")]
    public void Citation_WithoutADate_HasNone_NotADefaultDate(string json)
    {
        var citation = JsonSerializer.Deserialize<Citation>(json, Web)!;

        Assert.Null(citation.RetrievedAt);
    }

    [Fact]
    public void Citation_ToBrowser_WritesTheDateAsIso()
    {
        var json = JsonSerializer.Serialize(new Citation("8 CFR 214.13", null, null, new DateOnly(2026, 10, 5)), Web);

        Assert.Contains("\"retrievedAt\":\"2026-10-05\"", json);
    }
}
