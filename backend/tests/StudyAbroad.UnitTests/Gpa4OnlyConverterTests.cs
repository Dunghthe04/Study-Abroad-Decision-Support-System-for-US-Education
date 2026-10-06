using StudyAbroad.Application.Grading;

namespace StudyAbroad.UnitTests;

public class Gpa4OnlyConverterTests
{
    private readonly Gpa4OnlyConverter _c = new();

    [Theory]
    [InlineData(3.5, "4", 3.5)]       // thang 4 → dùng luôn
    [InlineData(4.3, "4", 4.0)]       // vượt 4 → giới hạn ở 4
    [InlineData(3.2, " 4 ", 3.2)]     // khoảng trắng thừa quanh thang điểm
    public async Task Scale4_ReturnsValue(double gpa, string scale, double expected) =>
        Assert.Equal((decimal)expected, await _c.ToGpa4Async((decimal)gpa, scale));

    [Theory]
    [InlineData(8.5, "10")]           // thang 10 → chưa quy đổi, không đoán
    [InlineData(85, "100")]
    public async Task OtherScales_ReturnNull(double gpa, string scale) =>
        Assert.Null(await _c.ToGpa4Async((decimal)gpa, scale));

    [Fact]
    public async Task NoGpa_ReturnsNull() => Assert.Null(await _c.ToGpa4Async(null, "4"));

    [Fact]
    public async Task NoScale_ReturnsNull() => Assert.Null(await _c.ToGpa4Async(3.5m, null));
}
