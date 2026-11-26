using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OtpNet;
using StudyAbroad.Infrastructure.Auth;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-03 3d] TOTP (Otp.NET) và mã hóa khóa bí mật AES-GCM.</summary>
public class TotpServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 6, 0, 15, DateTimeKind.Utc);

    private static TotpService Create(string? encryptionKey = null, string jwtSecret = "jwt-secret-for-tests-at-least-32-characters") =>
        new(Options.Create(new TotpOptions { EncryptionKey = encryptionKey }),
            Options.Create(new JwtOptions { SecretKey = jwtSecret }),
            NullLogger<TotpService>.Instance);

    private static string CodeAt(string secret, DateTime at) => new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(at);

    [Fact]
    public void GenerateSecret_IsBase32Of20Bytes_AndRandom()
    {
        var svc = Create();
        var a = svc.GenerateSecret();
        Assert.Equal(20, Base32Encoding.ToBytes(a).Length);
        Assert.NotEqual(a, svc.GenerateSecret());
    }

    [Fact]
    public void VerifyCode_AcceptsCurrentCode_AndOneStepEachSide()
    {
        var svc = Create();
        var secret = svc.GenerateSecret();
        Assert.True(svc.VerifyCode(secret, CodeAt(secret, Now), Now));
        Assert.True(svc.VerifyCode(secret, CodeAt(secret, Now.AddSeconds(-30)), Now));
        Assert.True(svc.VerifyCode(secret, CodeAt(secret, Now.AddSeconds(30)), Now));
    }

    [Fact]
    public void VerifyCode_RejectsCodeTwoStepsAway_WrongCode_AndBadFormat()
    {
        var svc = Create();
        var secret = svc.GenerateSecret();
        var old = CodeAt(secret, Now.AddSeconds(-90));
        if (old != CodeAt(secret, Now) && old != CodeAt(secret, Now.AddSeconds(-30)) && old != CodeAt(secret, Now.AddSeconds(30)))
            Assert.False(svc.VerifyCode(secret, old, Now));
        var right = CodeAt(secret, Now);
        var wrong = ((int.Parse(right) + 1) % 1_000_000).ToString("D6");
        Assert.False(svc.VerifyCode(secret, wrong, Now));
        Assert.False(svc.VerifyCode(secret, "12345", Now));
        Assert.False(svc.VerifyCode(secret, "abcdef", Now));
        Assert.False(svc.VerifyCode(secret, "", Now));
    }

    [Fact]
    public void VerifyCode_IgnoresSpaces()
    {
        var svc = Create();
        var secret = svc.GenerateSecret();
        var code = CodeAt(secret, Now);
        Assert.True(svc.VerifyCode(secret, $" {code[..3]} {code[3..]} ", Now));
    }

    [Fact]
    public void BuildOtpAuthUri_ContainsSecretIssuerAndAccount()
    {
        var svc = Create();
        var uri = svc.BuildOtpAuthUri("JBSWY3DPEHPK3PXP", "mod@example.com");
        Assert.StartsWith("otpauth://totp/", uri);
        Assert.Contains("secret=JBSWY3DPEHPK3PXP", uri);
        Assert.Contains("issuer=USAS", uri);
        Assert.Contains("mod", uri);
    }

    [Fact]
    public void Protect_RoundTrips_AndDoesNotStorePlainText()
    {
        var svc = Create();
        var secret = svc.GenerateSecret();
        var stored = svc.Protect(secret);
        Assert.DoesNotContain(secret, stored);
        Assert.NotEqual(stored, svc.Protect(secret)); // nonce ngẫu nhiên
        Assert.Equal(secret, svc.Unprotect(stored));
    }

    [Fact]
    public void Unprotect_WithOtherKey_OrTamperedData_ReturnsNull()
    {
        var stored = Create().Protect("JBSWY3DPEHPK3PXP");
        Assert.Null(Create(jwtSecret: "another-jwt-secret-for-tests-32-characters").Unprotect(stored));
        var bytes = Convert.FromBase64String(stored);
        bytes[^1] ^= 0xFF;
        Assert.Null(Create().Unprotect(Convert.ToBase64String(bytes)));
        Assert.Null(Create().Unprotect("not-base64!"));
    }

    [Fact]
    public void ConfiguredKey_IsUsed_AndMustBe32Bytes()
    {
        var key = Convert.ToBase64String(new byte[32]);
        var stored = Create(encryptionKey: key).Protect("JBSWY3DPEHPK3PXP");
        Assert.Equal("JBSWY3DPEHPK3PXP", Create(encryptionKey: key).Unprotect(stored));
        Assert.Null(Create().Unprotect(stored)); // khóa dẫn xuất từ JWT khác khóa cấu hình
        Assert.Throws<InvalidOperationException>(() => Create(encryptionKey: Convert.ToBase64String(new byte[16])));
    }
}
