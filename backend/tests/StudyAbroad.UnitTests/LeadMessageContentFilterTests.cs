using StudyAbroad.Application.Leads;
using Xunit;

namespace StudyAbroad.UnitTests;

public class LeadMessageContentFilterTests
{
    [Theory]
    [InlineData("Em gọi trực tiếp số 0912345678 để trao đổi nhé.")]
    [InlineData("Liên hệ với anh qua 0912 345 678 hoặc 0912.345.678")]
    [InlineData("Số văn phòng: (024) 3825-1234")]
    [InlineData("Gọi +84 912 345 678 giúp em")]
    [InlineData("Tổng đài hỗ trợ 1900 1234")]
    [InlineData("Hotline miễn phí 1800-567890")]
    [InlineData("Liên hệ US +1 (617) 555-0199")]
    [InlineData("Sdt của anh là 0987-654-321")]
    [InlineData("Tel: 090 123 456")]
    [InlineData("Số điện thoại zalo 0912*345*678")]
    [InlineData("Nhắn anh qua số 0912\u200B345\u200B678 nhé")]
    public void Inspect_WithPhoneNumber_FlagsMessage(string content)
    {
        var result = LeadMessageContentFilter.Inspect(content);

        Assert.True(result.IsFlagged);
        Assert.NotNull(result.FlagReason);
        Assert.Contains("số điện thoại", result.FlagReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Xem ưu đãi tại https://example.com/promo ngay nhé")]
    [InlineData("Truy cập www.quangcao.vn để nhận quà")]
    [InlineData("Nhấn vào zalo.me/0912345678 để trao đổi")]
    [InlineData("Chat qua wa.me/84912345678 hoặc m.me/trungtamduhoc")]
    [InlineData("Điền biểu mẫu tại forms.gle/abc123 hoặc docs.google.com/forms/d/e/1")]
    [InlineData("Xem thêm link liên hệ ở linktr.ee/trungtamus")]
    [InlineData("Trung tâm đang có chương trình khuyến mãi lớn cho học sinh đăng ký hôm nay")]
    [InlineData("Gói tư vấn đang giảm giá và tặng voucher 5 triệu")]
    [InlineData("Cam kết đậu 100% và bao đậu visa nếu đăng ký ngay")]
    [InlineData("Chuong trinh khuyen mai dac biet chi hom nay")]
    [InlineData("Em vui lòng chuyển khoản đặt cọc sớm ngoài hệ thống")]
    [InlineData("Chương trình khu\u200Byến mãi ưu đãi độc quyền")]
    public void Inspect_WithAdvertisement_FlagsMessage(string content)
    {
        var result = LeadMessageContentFilter.Inspect(content);

        Assert.True(result.IsFlagged);
        Assert.NotNull(result.FlagReason);
        Assert.Contains("quảng cáo", result.FlagReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inspect_WithBothPhoneAndAd_CombinesBothReasons()
    {
        var result = LeadMessageContentFilter.Inspect(
            "Chương trình khuyến mãi giảm giá, gọi ngay 0912345678 hoặc xem https://promo.example.com");

        Assert.True(result.IsFlagged);
        Assert.NotNull(result.FlagReason);
        Assert.Contains("số điện thoại", result.FlagReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("quảng cáo", result.FlagReason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \u200B  ")]
    [InlineData("Chào em, với GPA 3.85/4.0 và SAT 1520, em có cơ hội tốt vào nhóm trường Match.")]
    [InlineData("Học phí dự kiến khoảng $45,000/năm hoặc 500.000.000 VNĐ/năm trước học bổng.")]
    [InlineData("Em cần nộp IELTS 7.5 trước hạn 26/11/2026 cho kỳ Fall 2027.")]
    [InlineData("Call review deadline 15-11-2026 với cố vấn học thuật.")]
    [InlineData("Ngân hàng VIB có hỗ trợ chứng minh tài chính cho kỳ nhập học 2027.")]
    [InlineData("Mã hồ sơ nội bộ 123456789 chưa đủ điều kiện khớp số điện thoại.")]
    [InlineData("Trung tâm đã xem hồ sơ của em và gợi ý chuẩn bị thêm bài luận chính.")]
    public void Inspect_LegitimateOrEmptyContent_DoesNotFalsePositive(string? content)
    {
        var result = LeadMessageContentFilter.Inspect(content);

        Assert.False(result.IsFlagged);
        Assert.Null(result.FlagReason);
    }
}

