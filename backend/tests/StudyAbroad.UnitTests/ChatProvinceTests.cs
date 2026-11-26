using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Dashboard.Roadmap;

namespace StudyAbroad.UnitTests;

/// <summary>C4: học sinh trả lời tỉnh / thành trong chat, .NET kiểm tra và lưu như trang Tổng quan rồi viết lại câu trả lời.</summary>
public class ChatProvinceTests
{
    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement;

    private sealed class FakeRoadmap(SetProvinceResult result) : IRoadmapService
    {
        public List<string?> Saved { get; } = [];

        public Task<RoadmapDto?> GetRoadmapAsync(Guid studentId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<(bool Linked, RoadmapDto? Roadmap)> GetChildRoadmapAsync(Guid parentId, Guid studentId, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<SetProvinceResult> SetProvinceAsync(Guid studentId, string? province, CancellationToken ct = default)
        {
            Saved.Add(province);
            return Task.FromResult(result);
        }
    }

    private static AdvisorChatResponse Reply(string? actionData) => new(
        "Mình ghi nhận tỉnh / thành của bạn: tạm.", [], null,
        Action: ChatProvince.Action,
        ActionData: actionData is null ? null : Json(actionData));

    [Fact]
    public void ProvinceOf_ReadsTrimmedText()
    {
        Assert.Equal("Hà Nội", ChatProvince.ProvinceOf(Json("""{"province":"  Hà Nội "}""")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"province":"   "}""")]
    [InlineData("""{"province":5}""")]
    [InlineData("[]")]
    public void ProvinceOf_IsNullWhenThereIsNoUsableText(string json)
    {
        Assert.Null(ChatProvince.ProvinceOf(Json(json)));
    }

    [Fact]
    public async Task Saved_UsesTheCanonicalName()
    {
        var roadmap = new FakeRoadmap(SetProvinceResult.Saved);
        var userId = Guid.NewGuid();

        var res = await ChatProvince.SaveAsync(roadmap, userId, Reply("""{"province":"tp hcm"}"""));

        Assert.Equal(["tp hcm"], roadmap.Saved);
        Assert.Equal(ChatProvince.SavedAnswer("TP. Hồ Chí Minh"), res.Answer);
    }

    [Fact]
    public async Task Invalid_TellsTheStudentToPickOnDashboard()
    {
        var res = await ChatProvince.SaveAsync(new FakeRoadmap(SetProvinceResult.InvalidProvince), Guid.NewGuid(),
            Reply("""{"province":"Gotham"}"""));

        Assert.Equal(ChatProvince.InvalidAnswer("Gotham"), res.Answer);
    }

    [Fact]
    public async Task NoProfile_SaysSo()
    {
        var res = await ChatProvince.SaveAsync(new FakeRoadmap(SetProvinceResult.NoProfile), Guid.NewGuid(),
            Reply("""{"province":"Hà Nội"}"""));

        Assert.Equal(ChatProvince.NoProfileAnswer, res.Answer);
    }

    [Fact]
    public async Task EmptyProvince_NeverClearsTheSavedOne()
    {
        var roadmap = new FakeRoadmap(SetProvinceResult.Saved);
        var reply = Reply("""{"province":""}""");

        var res = await ChatProvince.SaveAsync(roadmap, Guid.NewGuid(), reply);

        Assert.Empty(roadmap.Saved);  // SetProvinceAsync nhận rỗng là xóa tỉnh
        Assert.Same(reply, res);
    }
}
