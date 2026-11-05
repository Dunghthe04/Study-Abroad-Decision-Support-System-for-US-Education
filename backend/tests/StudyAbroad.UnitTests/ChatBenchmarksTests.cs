using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

public class ChatBenchmarksTests
{
    // GPA = TB các trường (làm tròn 2 chữ số); tiếng Anh = mốc nhiều trường dùng nhất của từng chứng chỉ
    [Fact]
    public void GpaIsAverage_EnglishIsMostCommonMinimum()
    {
        var marks = ChatBenchmarks.From(
        [
            new BenchmarkRow(3.5m, 6.5m, 80m, 110m),
            new BenchmarkRow(3.8m, 6.5m, 90m, null),
            new BenchmarkRow(3.6m, 7m, 80m, null),
            new BenchmarkRow(null, null, null, null),
        ])!;

        Assert.Equal(3.63m, marks["gpa4"]!.GetValue<decimal>());
        var english = marks["english"]!;
        Assert.Equal(6.5m, english["ielts"]!.GetValue<decimal>());
        Assert.Equal(80m, english["toefl"]!.GetValue<decimal>());
        Assert.Equal(110m, english["duolingo"]!.GetValue<decimal>());
    }

    // Hòa số trường thì lấy mốc thấp hơn để kết quả luôn cố định
    [Fact]
    public void TieTakesLowerMark()
    {
        var marks = ChatBenchmarks.From([new BenchmarkRow(null, 7m, null, null), new BenchmarkRow(null, 6m, null, null)])!;

        Assert.Equal(6m, marks["english"]!["ielts"]!.GetValue<decimal>());
    }

    // Cột không trường nào có số thì bỏ khóa; không có gì thì null (chat chỉ ghi điểm)
    [Fact]
    public void MissingColumnsAreLeftOut()
    {
        var onlyGpa = ChatBenchmarks.From([new BenchmarkRow(3.4m, null, null, null)])!;

        Assert.Null(onlyGpa["english"]);
        Assert.Null(ChatBenchmarks.From([new BenchmarkRow(null, null, null, null)]));
        Assert.Null(ChatBenchmarks.From([]));
    }
}
