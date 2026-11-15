using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using StudyAbroad.Infrastructure.Persistence.Migrations;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SchoolDatasetRefresh091126MigrationTests
{
    private static readonly Lazy<AddCampusAndGpaWeightFieldsAndRefresh091126> MigrationInstance = new(() => new());

    private static readonly Lazy<string> RefreshSql = new(() =>
    {
        var sqlOp = MigrationInstance.Value.UpOperations.OfType<SqlOperation>().Single();
        return sqlOp.Sql;
    });

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
    public void Refresh091126_EmbedsAddColumnsAndSqlOperation()
    {
        var addColumns = MigrationInstance.Value.UpOperations.OfType<AddColumnOperation>().ToList();
        var addedColumnNames = addColumns.Select(c => c.Name).ToHashSet();

        var expectedNewColumns = new[]
        {
            "avg_gpa_weighted", "gpa_scale_type", "fy_on_campus_pct", "ug_on_campus_pct",
            "housing_types", "intl_housing", "housing_only_usd", "housing_deposit_usd",
            "student_faculty_ratio", "activities", "campus_setting"
        };

        foreach (var col in expectedNewColumns)
        {
            Assert.Contains(col, addedColumnNames);
        }

        var constraints = MigrationInstance.Value.UpOperations.OfType<AddCheckConstraintOperation>().ToList();
        var constraintNames = constraints.Select(c => c.Name).ToHashSet();
        Assert.Contains("ck_university_offerings_avg_gpa_weighted", constraintNames);
        Assert.Contains("ck_university_offerings_gpa_scale_type", constraintNames);

        Assert.Contains("UPDATE app.universities target", RefreshSql.Value);
        Assert.Contains("UPDATE app.university_offerings target", RefreshSql.Value);
        Assert.Contains("avg_gpa_weighted = (incoming.row_data).avg_gpa_weighted", RefreshSql.Value);
        Assert.Contains("gpa_scale_type = (incoming.row_data).gpa_scale_type", RefreshSql.Value);
        Assert.Contains("campus_setting = (incoming.row_data).campus_setting", RefreshSql.Value);
        Assert.DoesNotContain("DELETE FROM app.universities", RefreshSql.Value);
        Assert.DoesNotContain("DELETE FROM app.university_offerings", RefreshSql.Value);
        Assert.Contains("ON CONFLICT (id) DO NOTHING", RefreshSql.Value);
    }

    [Fact]
    public void Refresh091126_Contains249UniqueSchoolsWithoutNullReplacements()
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
    public void Refresh091126_PreservesConflictingAidAndWeightedGpaInsteadOfInventingValues()
    {
        var rows = ReadSchools();
        var conflict = Assert.Single(rows, row => row.Code == "100858");
        foreach (var column in new[] { "intl_aid_policy", "intl_aid_avg_usd", "intl_aid_recipients" })
            Assert.False(conflict.Offering.TryGetProperty(column, out _));

        foreach (var code in new[] { "110644", "132903", "137351", "144050", "215293", "221759", "433660" })
        {
            var offering = Assert.Single(rows, row => row.Code == code).Offering;
            Assert.False(offering.TryGetProperty("avg_gpa4", out _));
            Assert.True(offering.TryGetProperty("avg_gpa_weighted", out var gpaw));
            Assert.True(gpaw.GetDecimal() > 4.0m);
            Assert.True(offering.TryGetProperty("gpa_scale_type", out var scale));
            Assert.Equal("weighted", scale.GetString());
        }

        foreach (var code in new[] { "136172", "433660" })
        {
            var offering = Assert.Single(rows, row => row.Code == code).Offering;
            Assert.DoesNotContain(offering.EnumerateObject(), property => property.Name.StartsWith("gpa_pct_", StringComparison.Ordinal));
        }

        Assert.Contains("preserve_existing_database_aid_group", RefreshSql.Value);
        Assert.Contains("\"status\":\"unverified\"", RefreshSql.Value);
    }

    [Fact]
    public void Refresh091126_NumericFieldsRespectExistingScales()
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
                if (property.Name == "avg_gpa_weighted")
                    Assert.True(property.Value.GetDecimal() >= 0m);
                if (property.Name == "student_faculty_ratio")
                    Assert.True(property.Value.GetInt32() > 0);
            }
        }
    }

    [Fact]
    public void Refresh091126_PopulatesCampusAndHousingFields()
    {
        var rows = ReadSchools();

        var withCampusSetting = rows.Count(r => r.Offering.TryGetProperty("campus_setting", out _));
        Assert.Equal(249, withCampusSetting);

        var withHousingTypes = rows.Count(r => r.Offering.TryGetProperty("housing_types", out _));
        Assert.True(withHousingTypes >= 190);

        var withIntlHousing = rows.Count(r => r.Offering.TryGetProperty("intl_housing", out _));
        Assert.True(withIntlHousing >= 190);

        var withActivities = rows.Count(r => r.Offering.TryGetProperty("activities", out _));
        Assert.True(withActivities >= 190);

        var withSfr = rows.Count(r => r.Offering.TryGetProperty("student_faculty_ratio", out _));
        Assert.True(withSfr >= 220);
    }

    [Fact]
    public void Refresh091126_RefusesLossyAutomaticRollback()
    {
        Assert.Throws<NotSupportedException>(() => new AddCampusAndGpaWeightFieldsAndRefresh091126().DownOperations);
    }
}
