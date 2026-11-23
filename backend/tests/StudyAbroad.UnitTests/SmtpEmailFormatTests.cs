using System;
using StudyAbroad.Infrastructure.Auth;
using Xunit;

namespace StudyAbroad.UnitTests;

public class SmtpEmailFormatTests
{
    [Theory]
    [InlineData(79000, "79.000 đ")]
    [InlineData(1234567, "1.234.567 đ")]
    [InlineData(500, "500 đ")]
    [InlineData(0, "0 đ")]
    public void Vnd_UsesDotGroupingWithoutDecimals(int amount, string expected) =>
        Assert.Equal(expected, SmtpEmailSender.Vnd(amount));

    [Fact]
    public void VietnamTime_AddsSevenHours_AndCrossesMidnight() =>
        Assert.Equal("21/11/2026 01:05", SmtpEmailSender.VietnamTime(new DateTime(2026, 11, 20, 18, 5, 0, DateTimeKind.Utc)));
}
