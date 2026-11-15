using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using StudyAbroad.Infrastructure.Persistence.Migrations;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SchoolDatasetRefreshMigrationTests
{
    private static readonly Lazy<string> RefreshSql = new(() =>
        Assert.IsType<SqlOperation>(Assert.Single(new RefreshSchoolDataset051126().UpOperations)).Sql);

    private static List<(string Code, JsonElement University, JsonElement Offering)> ReadSchools()
    {
        var pattern = new Regex(@"^\('(?<code>\d+)', '(?<university>(?:[^']|'')*)'::jsonb, '(?<offering>(?:[^']|'')*)'::jsonb\)[,;]\r?$", RegexOptions.Multiline);
        var rows = new List<(string, JsonElement, JsonElement)>();
        foreach (Match match in pattern.Matches(RefreshSql.Value))
        {
            using var university = JsonDocument.Parse(match.Groups["university"].Value.Replace("''", "'"));
            using var offering = JsonDocument.Parse(match.Groups["offering"].Value.Replace("''", "'"));
            rows.Add((match.Groups["code"].Value, university.RootElement.Clone(), offering.RootElement.Clone()));
        }
        return rows;
    }

    [Fact]
    public void Refresh_EmbedsOneDataOnlySqlOperation()
    {
        Assert.Contains("UPDATE app.universities target", RefreshSql.Value);
        Assert.Contains("UPDATE app.university_offerings target", RefreshSql.Value);
        Assert.DoesNotContain("DELETE FROM app.universities", RefreshSql.Value);
        Assert.DoesNotContain("DELETE FROM app.university_offerings", RefreshSql.Value);
        Assert.Contains("ON CONFLICT (id) DO NOTHING", RefreshSql.Value);
    }

    [Fact]
    public void Refresh_Contains249UniqueSchoolsWithoutNullReplacements()
    {
        var rows = ReadSchools();
        Assert.Equal(249, rows.Count);
        Assert.Equal(249, rows.Select(row => row.Code).Distinct().Count());
        foreach (var row in rows)
        {
            Assert.All(row.University.EnumerateObject(), property => Assert.NotEqual(JsonValueKind.Null, property.Value.ValueKind));
            Assert.All(row.Offering.EnumerateObject(), property => Assert.NotEqual(JsonValueKind.Null, property.Value.ValueKind));
        }
    }

    [Fact]
    public void Refresh_PreservesConflictingAidAndWeightedGpaInsteadOfInventingValues()
    {
        var rows = ReadSchools();
        var conflict = Assert.Single(rows, row => row.Code == "100858");
        foreach (var column in new[] { "intl_aid_policy", "intl_aid_avg_usd", "intl_aid_recipients" })
            Assert.False(conflict.Offering.TryGetProperty(column, out _));
        foreach (var code in new[] { "110644", "132903", "137351", "144050", "215293", "221759", "433660" })
            Assert.False(Assert.Single(rows, row => row.Code == code).Offering.TryGetProperty("avg_gpa4", out _));
        foreach (var code in new[] { "136172", "433660" })
        {
            var offering = Assert.Single(rows, row => row.Code == code).Offering;
            Assert.DoesNotContain(offering.EnumerateObject(), property => property.Name.StartsWith("gpa_pct_", StringComparison.Ordinal));
        }
        Assert.Contains("preserve_existing_database_aid_group", RefreshSql.Value);
        Assert.Contains("\"status\":\"unverified\"", RefreshSql.Value);
    }

    [Fact]
    public void Refresh_NumericFieldsRespectExistingScales()
    {
        foreach (var row in ReadSchools())
        {
            if (row.University.TryGetProperty("acceptance_rate", out var acceptance))
                Assert.InRange(acceptance.GetDecimal(), 0m, 1m);
            foreach (var property in row.Offering.EnumerateObject())
            {
                if (property.Name.Contains("_pct", StringComparison.Ordinal) || property.Name is "intl_acceptance_rate" or "retention_rate_1yr" or "grad_rate_6yr")
                    Assert.InRange(property.Value.GetDecimal(), 0m, 1m);
                if (property.Name is "min_gpa4" or "avg_gpa4")
                    Assert.InRange(property.Value.GetDecimal(), 0m, 4m);
            }
        }
    }

    [Fact]
    public void Refresh_RefusesLossyAutomaticRollback()
    {
        Assert.Throws<NotSupportedException>(() => new RefreshSchoolDataset051126().DownOperations);
    }
}
