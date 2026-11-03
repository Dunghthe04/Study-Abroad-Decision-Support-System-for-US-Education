using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

/// <summary>Hỏi giải thích một trường trong chat: đọc mã trường advisor chọn và dựng câu trả lời.</summary>
public class ChatExplanationTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    [Fact]
    public void CodeOf_ReadsTheSchoolCode()
    {
        Assert.Equal("iowa", ChatExplanation.CodeOf(Json("""{"code":"iowa"}""")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"code":null}""")]
    [InlineData("""{"code":5}""")]
    [InlineData("[]")]
    public void CodeOf_IsNullWhenThereIsNoUsableCode(string json)
    {
        Assert.Null(ChatExplanation.CodeOf(Json(json)));
    }

    [Fact]
    public void CodeOf_IsNullWhenThereIsNoData()
    {
        Assert.Null(ChatExplanation.CodeOf(null));
    }

    [Fact]
    public void Format_PutsSummaryFirst_ThenStrengthsAndImprovements()
    {
        var text = ChatExplanation.Format(new SchoolExplanation("iowa", "University of Iowa", "OK", "Tóm tắt.", ["Mạnh 1", "Mạnh 2"], ["Sửa 1"]));

        Assert.Equal(
            "University of Iowa: Tóm tắt.\n\n" +
            "Điểm mạnh của hồ sơ với trường này:\n- Mạnh 1\n- Mạnh 2\n\n" +
            "Bạn có thể cải thiện:\n- Sửa 1",
            text);
    }

    [Fact]
    public void Format_SkipsEmptySections()
    {
        var text = ChatExplanation.Format(new SchoolExplanation("iowa", "University of Iowa", "OK", "Tóm tắt.", [], ["Sửa 1"]));

        Assert.Equal("University of Iowa: Tóm tắt.\n\nBạn có thể cải thiện:\n- Sửa 1", text);
    }
}
