using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Infrastructure.Persistence;
using ChatMessageEntity = StudyAbroad.Domain.Entities.ChatMessage;

namespace StudyAbroad.UnitTests;

// Ảnh minh họa của câu trả lời: link do advisor lấy từ cơ sở dữ liệu nhưng .NET vẫn kiểm lại; lưu vào chat_messages.media_json
public class ChatMediaTests
{
    private static MediaItem Img(string url, string alt = "Mẫu I-20") => new("image", url, alt);

    [Theory]
    [InlineData("https://travel.state.gov/mau-i20.png")]
    [InlineData("https://a.edu/x.png?size=large&v=2")]
    [InlineData("https://sub.domain.vn:8443/anh/i20.jpg")]
    public void Clean_KeepsHttpsLinks(string url) => Assert.Equal([Img(url)], ChatMedia.Clean([Img(url)]));

    [Theory]
    [InlineData("http://a.edu/x.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,AAAA")]
    [InlineData("ftp://a.edu/x.png")]
    [InlineData("//a.edu/x.png")]
    [InlineData("a.edu/x.png")]
    [InlineData("https://")]
    [InlineData("https://a.edu/hai chu.png")]
    [InlineData("")]
    [InlineData("   ")]
    public void Clean_DropsEverythingThatIsNotAnAbsoluteHttpsLink(string url) => Assert.Null(ChatMedia.Clean([Img(url)]));

    [Fact]
    public void Clean_DropsNonImagesDuplicatesAndKeepsAtMostThree()
    {
        var items = new List<MediaItem>
        {
            new("video", "https://a.edu/v.mp4", "video"),
            Img("https://a.edu/1.png"),
            Img("https://a.edu/1.png"),
            Img("https://a.edu/2.png"),
            Img("https://a.edu/3.png"),
            Img("https://a.edu/4.png"),
        };

        var urls = ChatMedia.Clean(items)!.Select(m => m.Url);

        Assert.Equal(["https://a.edu/1.png", "https://a.edu/2.png", "https://a.edu/3.png"], urls);
    }

    [Fact]
    public void Clean_GivesAMissingAltADefault_AndCutsAVeryLongOne()
    {
        var clean = ChatMedia.Clean([Img("https://a.edu/1.png", "  "), Img("https://a.edu/2.png", new string('x', 1000))])!;

        Assert.Equal(ChatMedia.DefaultAlt, clean[0].Alt);
        Assert.Equal(ChatMedia.MaxAltLength, clean[1].Alt.Length);
    }

    [Fact]
    public void Clean_OfNothingIsNull_SoTheBrowserNeverGetsAnEmptyList()
    {
        Assert.Null(ChatMedia.Clean(null));
        Assert.Null(ChatMedia.Clean([]));
    }

    [Fact]
    public void ToJson_ThenFromJson_RoundTrips()
    {
        var json = ChatMedia.ToJson([Img("https://a.edu/1.png")]);

        Assert.Equal([Img("https://a.edu/1.png")], ChatMedia.FromJson(json));
        Assert.Contains("\"kind\":\"image\"", json);   // khóa viết thường như frontend đọc
    }

    [Fact]
    public void ToJson_OfNoValidImageIsNull_SoTheColumnStaysEmpty() => Assert.Null(ChatMedia.ToJson([Img("http://a.edu/x.png")]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{không phải json")]
    [InlineData("{}")]
    [InlineData("\"một chuỗi\"")]
    public void FromJson_OfNothingOrBrokenData_IsEmptyNotAnError(string? json) => Assert.Empty(ChatMedia.FromJson(json));

    [Fact]
    public void FromJson_ChecksTheStoredLinksAgain()
    {
        var stored = """[{"kind":"image","url":"javascript:alert(1)","alt":"x"},{"kind":"image","url":"https://a.edu/ok.png","alt":"ok"}]""";

        var items = ChatMedia.FromJson(stored);

        Assert.Equal("https://a.edu/ok.png", Assert.Single(items).Url);
    }

    [Fact]
    public void TheMediaColumn_IsAnOptionalJsonbOnChatMessages()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options);
        var entity = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ChatMessageEntity))!;

        var column = entity.FindProperty(nameof(ChatMessageEntity.MediaJson))!;

        Assert.True(column.IsNullable);   // tin cũ và tin không có ảnh để trống
        Assert.Equal("jsonb", column.GetColumnType());
        Assert.Equal("media_json", column.GetColumnName());
    }
}
