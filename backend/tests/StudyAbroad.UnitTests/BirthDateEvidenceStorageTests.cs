using System.Net;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudyAbroad.Infrastructure.Auth;
using StudyAbroad.Infrastructure.MediaUploads;
using StudyAbroad.Infrastructure.Services;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-107 / UC-150] Minh chứng ngày sinh mã hóa AES-GCM trước khi lên Cloudinary.</summary>
public class BirthDateEvidenceStorageTests
{
    private const string Prefix = "usas/birth-date-evidence/";
    private static readonly string Path1 = $"{Prefix}{Guid.NewGuid():N}/{Guid.NewGuid():N}";
    private static readonly string Path2 = $"{Prefix}{Guid.NewGuid():N}/{Guid.NewGuid():N}";

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static BirthDateEvidenceStorage Create(string? key = null, HttpMessageHandler? handler = null) => new(
        new HttpClient(handler ?? new CountingHandler()),
        Options.Create(new CloudinaryOptions { CloudName = "demo", ApiKey = "k", ApiSecret = "s", Folder = "usas" }),
        Options.Create(new BirthDateEvidenceOptions { EncryptionKey = key ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) }),
        Options.Create(new JwtOptions()),
        NullLogger<BirthDateEvidenceStorage>.Instance);

    [Fact]
    public void Encrypt_RoundTrip_ReturnsOriginal_AndCipherHasNoPlaintext()
    {
        var storage = Create();
        var plain = "%PDF-1.4 giay khai sinh"u8.ToArray();

        var sealedBytes = storage.Encrypt(plain, Path1);

        Assert.Equal(plain, storage.Decrypt(sealedBytes, Path1));
        Assert.Equal(-1, sealedBytes.AsSpan().IndexOf("%PDF"u8));
    }

    [Fact]
    public void Decrypt_CopiedToOtherPath_ReturnsNull()
    {
        var storage = Create();
        var sealedBytes = storage.Encrypt([1, 2, 3, 4], Path1);

        Assert.Null(storage.Decrypt(sealedBytes, Path2));
    }

    [Fact]
    public void Decrypt_Tampered_ReturnsNull()
    {
        var storage = Create();
        var sealedBytes = storage.Encrypt([1, 2, 3, 4], Path1);
        sealedBytes[20] ^= 0xFF;

        Assert.Null(storage.Decrypt(sealedBytes, Path1));
    }

    [Fact]
    public void Decrypt_WithOtherKey_ReturnsNull()
    {
        var sealedBytes = Create().Encrypt([1, 2, 3, 4], Path1);

        Assert.Null(Create().Decrypt(sealedBytes, Path1));
    }

    [Fact]
    public void Encrypt_WrongKeyLength_Throws()
    {
        var storage = Create(Convert.ToBase64String(new byte[16]));

        Assert.Throws<InvalidOperationException>(() => storage.Encrypt([1], Path1));
    }

    [Theory]
    [InlineData("usas/other/aaaa")]
    [InlineData("usas/birth-date-evidence/../center-docs/x")]
    [InlineData("uploads/birth-date-evidence/abc/file.pdf")]
    public async Task OpenAsync_ForeignPath_ReturnsNull_WithoutCallingCloudinary(string path)
    {
        var handler = new CountingHandler();
        var storage = Create(handler: handler);

        Assert.Null(await storage.OpenAsync(path));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task OpenAsync_MissingOnCloudinary_ReturnsNull()
    {
        var handler = new CountingHandler();
        var storage = Create(handler: handler);

        Assert.Null(await storage.OpenAsync(Path1));
        Assert.Equal(1, handler.Calls);
    }
}
