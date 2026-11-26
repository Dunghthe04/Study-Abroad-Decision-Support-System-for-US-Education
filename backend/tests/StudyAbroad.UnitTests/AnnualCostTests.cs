using StudyAbroad.Application.Costs;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

public class AnnualCostTests
{
    [Fact]
    public void Of_SumsAllFiveItemsIncludingInsurance()
    {
        Assert.Equal(75_000m, AnnualCost.Of(50_000m, 20_000m, 2_000m, null, 3_000m));
    }

    [Fact]
    public void Of_NoTuition_HasNoTotal()
    {
        Assert.Null(AnnualCost.Of(null, 20_000m, 2_000m, 500m, 3_000m));
    }

    [Fact]
    public void Sql_MatchesOf()
    {
        var o = new UniversityOffering { TuitionUsd = 50_000m, LivingUsd = 20_000m, HealthInsuranceUsd = 3_000m };
        Assert.Equal(AnnualCost.Of(o), AnnualCost.Sql.Compile()(o));
        Assert.Null(AnnualCost.Sql.Compile()(new UniversityOffering { LivingUsd = 20_000m }));
    }
}
