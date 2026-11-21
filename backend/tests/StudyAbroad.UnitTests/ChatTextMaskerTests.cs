using StudyAbroad.Application.Admin.AiQuality;

namespace StudyAbroad.UnitTests;

// UC-90: che thông tin cá nhân trong nội dung chat trước khi admin đọc mẫu hội thoại.
public class ChatTextMaskerTests
{
    private static string Mask(string text, params string[] names) => ChatTextMasker.Mask(text, names);

    // ---- từng loại thông tin

    [Theory]
    [InlineData("Email em là minh.nguyen@gmail.com nhé", "Email em là [EMAIL] nhé")]
    [InlineData("liên hệ: Lan_Pham99@sinhvien.edu.vn.", "liên hệ: [EMAIL].")]
    public void Emails_AreMasked(string text, string expected) => Assert.Equal(expected, Mask(text));

    [Theory]
    [InlineData("Số em là 0912345678 ạ")]
    [InlineData("Số em là 0912 345 678 ạ")]
    [InlineData("Số em là 091.234.5678 ạ")]
    [InlineData("Số em là 091-234-5678 ạ")]
    [InlineData("Số em là +84912345678 ạ")]
    [InlineData("Số em là +84 912 345 678 ạ")]
    [InlineData("Số em là 84912345678 ạ")]
    [InlineData("Số em là +1 415 555 0132 ạ")]
    public void PhoneNumbers_AreMasked_InTheCommonFormats(string text) =>
        Assert.Equal("Số em là [SĐT] ạ", Mask(text));

    [Fact]
    public void LongIdNumbers_AreMasked_ButNotAsPhones()
    {
        Assert.Equal("CCCD [MÃ SỐ]", Mask("CCCD 079203001234"));
        Assert.Equal("hộ chiếu [MÃ SỐ]", Mask("hộ chiếu 123456789"));
    }

    [Theory]
    [InlineData("Em sinh ngày 12/05/2008", "Em sinh ngày [NGÀY]")]
    [InlineData("sinh 12-05-2008 ở Hà Nội", "sinh [NGÀY] ở Hà Nội")]
    [InlineData("sinh 1.5.2008", "sinh [NGÀY]")]
    public void FullDatesWithAYear_AreMasked(string text, string expected) => Assert.Equal(expected, Mask(text));

    [Theory]
    [InlineData("Họ và tên: Nguyễn Văn An", "Họ và tên: [HỌ TÊN]")]
    [InlineData("em tên là Phạm Thị Lan Anh, học lớp 11", "em tên là [HỌ TÊN], học lớp 11")]
    [InlineData("Tên em là Minh", "Tên em là [HỌ TÊN]")]
    [InlineData("my name is Linh Tran", "my name is [HỌ TÊN]")]
    public void NamesAfterAnIntroduction_AreMasked_AndTheLabelStays(string text, string expected) =>
        Assert.Equal(expected, Mask(text));

    [Theory]
    [InlineData("facebook.com/minh.nguyen.123")]
    [InlineData("https://www.facebook.com/profile.php?id=100012345")]
    [InlineData("https://zalo.me/0912345678")]
    [InlineData("instagram.com/linh_tran")]
    public void SocialProfileLinks_AreMasked(string link) =>
        Assert.Equal("Trang của em: [LIÊN KẾT MẠNG XÃ HỘI]", Mask($"Trang của em: {link}"));

    // ---- phải để nguyên: admin cần các số này để đánh giá câu trả lời

    [Theory]
    [InlineData("GPA của em là 3.5 trên thang 4")]
    [InlineData("SAT 1400, IELTS 6.5, TOEFL 95")]
    [InlineData("Học phí khoảng 59.000 USD một năm, tổng 86,000 USD")]
    [InlineData("năm học 2025-2026, nộp hạn 1/11")]
    [InlineData("kỳ nhập học tháng 9/2026")]
    [InlineData("em học lớp 11, dự định thi SAT lần 2")]
    [InlineData("Trường nhận 4% hồ sơ, xếp hạng 12 trên 200")]
    [InlineData("Hiện hệ thống chưa có thông tin chắc chắn về vấn đề này.")]
    public void TheNumbersAndWordsNeededToJudgeTheAnswer_AreLeftAlone(string text) =>
        Assert.Equal(text, Mask(text));

    // ---- họ tên đã biết của chủ phiên chat

    [Fact]
    public void TheKnownFullName_IsMaskedAnywhere_EvenWithoutAnIntroduction()
    {
        var masked = Mask("Chào Nguyễn Văn An, hồ sơ của nguyễn  văn an đã lưu", "Nguyễn Văn An");

        Assert.Equal("Chào [HỌ TÊN], hồ sơ của [HỌ TÊN] đã lưu", masked);
    }

    [Fact]
    public void EachPartOfTheKnownName_IsMasked_AsAWholeWordWithTheSameCase()
    {
        var masked = Mask("Anh Minh hỏi, bạn Văn bảo Minh là người đó", "Trần Văn Minh");

        Assert.Equal("Anh [HỌ TÊN] hỏi, bạn [HỌ TÊN] bảo [HỌ TÊN] là người đó", masked);
    }

    [Fact]
    public void ANamePartThatIsAlsoAWord_OnlyMasksTheCapitalisedForm_NotTheLowercaseWord()
    {
        var masked = Mask("Em An hỏi: trường nào an toàn?", "Lê An");

        Assert.Equal("Em [HỌ TÊN] hỏi: trường nào an toàn?", masked);
    }

    [Fact]
    public void ANameThatContainsRegexCharacters_DoesNotBreakTheMasker()
    {
        Assert.Equal("[HỌ TÊN] hỏi", Mask("A.B+(C) hỏi", "A.B+(C)"));
        Assert.Equal("giữ nguyên", Mask("giữ nguyên", "***", "(", "  ", ""));
    }

    [Fact]
    public void BlankKnownNames_AreIgnored() =>
        Assert.Equal("Anh hỏi", ChatTextMasker.Mask("Anh hỏi", [null, "", "   "]));

    // ---- tính chất chung

    [Fact]
    public void AMessageWithEverything_IsMaskedPiece_ByPiece()
    {
        var text = "Em là Trần Văn Minh, sinh 12/05/2008, email minh@gmail.com, sđt 0912 345 678, CCCD 079203001234, GPA 3.5, SAT 1400.";

        var masked = Mask(text, "Trần Văn Minh");

        Assert.Equal("Em là [HỌ TÊN], sinh [NGÀY], email [EMAIL], sđt [SĐT], CCCD [MÃ SỐ], GPA 3.5, SAT 1400.", masked);
    }

    [Fact]
    public void MaskingTwice_GivesTheSameResult()
    {
        var once = Mask("Họ và tên: Lê An, 0912345678, a@b.com, 12/05/2008", "Lê An");

        Assert.Equal(once, Mask(once, "Lê An"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NothingToMask_GivesAnEmptyString(string? text) => Assert.Equal(string.Empty, ChatTextMasker.Mask(text));

    [Fact]
    public void ALongOddInput_FinishesQuickly_WithoutHangingOnTheRules()
    {
        var hostile = new string('0', 50_000) + string.Concat(Enumerable.Repeat("a.", 20_000)) + string.Concat(Enumerable.Repeat("Aa ", 20_000));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        ChatTextMasker.Mask(hostile, ["Trần Văn Minh"]);

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"mất {watch.Elapsed}");
    }
}
