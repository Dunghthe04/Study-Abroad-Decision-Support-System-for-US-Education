using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class AdmissionCategorizerTests
{
    // Trường mẫu: GPA trung bình 3.4, SAT 1300–1450
    [Theory]
    [InlineData(3.8, 1500, AdmissionCategory.Safety)]   // cả hai cao
    [InlineData(3.5, 1350, AdmissionCategory.Match)]    // cả hai ngang
    [InlineData(3.0, 1200, AdmissionCategory.Reach)]    // cả hai thấp
    [InlineData(3.8, 1350, AdmissionCategory.Match)]    // GPA cao + SAT ngang → lấy mức thấp hơn
    [InlineData(3.5, 1200, AdmissionCategory.Reach)]    // GPA ngang + SAT thấp → Reach
    [InlineData(3.7, 1450, AdmissionCategory.Safety)]   // chênh đúng 0.3 và SAT đúng mốc 75% → đều tính là cao
    [InlineData(3.1, 1300, AdmissionCategory.Reach)]    // kém đúng 0.3 → thấp; SAT đúng mốc 25% → ngang
    public void Categorize_WithFullData(double gpa, int sat, AdmissionCategory expected)
    {
        var result = AdmissionCategorizer.Categorize((decimal)gpa, sat, 3.4m, 1300, 1450,  0.3m);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Categorize_OnlySat_UsesSat() =>
        Assert.Equal(AdmissionCategory.Reach,
            AdmissionCategorizer.Categorize(3.9m, 1200, avgGpa4: null, 1300, 1450, 0.3m));

    [Fact]
    public void Categorize_OnlyGpa_UsesGpa() =>
        Assert.Equal(AdmissionCategory.Safety,
            AdmissionCategorizer.Categorize(3.9m, studentSat: null, 3.4m, 1300, 1450, 0.3m));

    [Fact]
    public void Categorize_NoComparableData_ReturnsInsufficientData()
    {
        // trường không công bố gì
        Assert.Equal(AdmissionCategory.InsufficientData,
            AdmissionCategorizer.Categorize(3.5m, 1300, null, null, null, 0.3m));
        // học sinh chưa nhập GPA và SAT
        Assert.Equal(AdmissionCategory.InsufficientData,
            AdmissionCategorizer.Categorize(null, null, 3.4m, 1300, 1450, 0.3m));
    }

    [Fact]
    public void Categorize_ChangingBand_ChangesResult()   // tiêu chí: đổi ngưỡng thì kết quả đổi theo
    {
        Assert.Equal(AdmissionCategory.Match, AdmissionCategorizer.Categorize(3.6m, null, 3.4m, null, null, 0.3m));
        Assert.Equal(AdmissionCategory.Safety, AdmissionCategorizer.Categorize(3.6m, null, 3.4m, null, null, 0.1m));
    }

    [Fact]
    public void Categorize_SameInput_SameResult()          // tiêu chí: chạy lại cho cùng kết quả
    {
        var first = AdmissionCategorizer.Categorize(3.5m, 1320, 3.4m, 1300, 1450, 0.3m);
        for (var i = 0; i < 100; i++)
            Assert.Equal(first, AdmissionCategorizer.Categorize(3.5m, 1320, 3.4m, 1300, 1450, 0.3m));
    }
}
