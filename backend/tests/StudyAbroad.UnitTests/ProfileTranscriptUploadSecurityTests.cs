using System.Reflection;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Api.RateLimiting;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Infrastructure.Services;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-640] Bảo mật upload bảng điểm: kiểm tra nội dung thật của tệp, chỉ xoá tệp trong thư mục của chính user,
/// controller chỉ nhận 1 tệp ≤ 10MB và có giới hạn tần suất.
/// </summary>
public class ProfileTranscriptUploadSecurityTests : IDisposable
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7\n%%EOF");
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10];

    private readonly string _root = Path.Combine(Path.GetTempPath(), "usas-upload-tests-" + Guid.NewGuid().ToString("N"));
    private readonly ProfileFileStorageService _storage;

    public ProfileTranscriptUploadSecurityTests()
    {
        Directory.CreateDirectory(_root);
        _storage = new ProfileFileStorageService(new TestHostEnvironment(_root));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private Task<DraftTranscriptInfoDto> Save(Guid userId, byte[] bytes, string fileName, string contentType = "application/octet-stream") =>
        _storage.SaveDraftTranscriptAsync(userId, new MemoryStream(bytes), fileName, contentType, bytes.Length);

    private string FullPath(string relativePath) =>
        Path.Combine(_root, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

    // ───────────── Storage: kiểm tra nội dung thật ─────────────

    [Theory]
    [InlineData("bang_diem.pdf", "application/pdf")]
    [InlineData("bang_diem.PNG", "image/png")]
    [InlineData("bang_diem.jpeg", "image/jpeg")]
    [InlineData("bang_diem.jpg", "image/jpeg")]
    public async Task Save_ValidFile_StoresWholeContent_AndUsesDetectedContentType(string fileName, string expectedType)
    {
        var bytes = expectedType switch
        {
            "application/pdf" => PdfBytes,
            "image/png" => PngBytes,
            _ => JpegBytes,
        };

        // Content type trình duyệt gửi lên sai cũng không được lưu lại
        var info = await Save(Guid.NewGuid(), bytes, fileName, contentType: "text/html");

        Assert.Equal(expectedType, info.ContentType);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(FullPath(info.RelativePath)));
    }

    [Fact]
    public async Task Save_HtmlDisguisedAsPng_IsRejected_AndNothingStored()
    {
        var userId = Guid.NewGuid();
        var html = Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

        await Assert.ThrowsAsync<ArgumentException>(() => Save(userId, html, "anh.png", "image/png"));

        Assert.False(Directory.Exists(Path.Combine(_root, "uploads", "drafts", userId.ToString()))
            && Directory.EnumerateFiles(Path.Combine(_root, "uploads", "drafts", userId.ToString())).Any());
    }

    [Fact]
    public async Task Save_JpegContentWithPdfExtension_IsRejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Save(Guid.NewGuid(), JpegBytes, "bang_diem.pdf"));

    [Theory]
    [InlineData("script.svg")]
    [InlineData("trang.html")]
    [InlineData("anh.webp")]
    [InlineData("khong_duoi")]
    public async Task Save_DisallowedExtension_IsRejected(string fileName) =>
        await Assert.ThrowsAsync<ArgumentException>(() => Save(Guid.NewGuid(), PdfBytes, fileName));

    [Fact]
    public async Task Save_TooShortFile_IsRejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => Save(Guid.NewGuid(), [0x25, 0x50], "a.pdf"));

    [Fact]
    public async Task Save_OverSizeLimit_IsRejected() =>
        await Assert.ThrowsAsync<ArgumentException>(() => _storage.SaveDraftTranscriptAsync(
            Guid.NewGuid(), new MemoryStream(PdfBytes), "a.pdf", "application/pdf", ProfileFileStorageLimits.MaxFileSizeBytes + 1));

    [Fact]
    public async Task Save_UsesRandomStoredName_NotClientFileName()
    {
        var info = await Save(Guid.NewGuid(), PdfBytes, "..\\..\\evil.pdf");

        Assert.Equal("evil.pdf", Path.GetFileName(info.OriginalFileName.Replace('\\', '/')));
        Assert.DoesNotContain("evil", info.StoredFileName);
        Assert.DoesNotContain("..", info.RelativePath);
    }

    // ───────────── Storage: xoá tệp cũ ─────────────

    [Fact]
    public async Task DeleteDraftFile_RemovesOnlyThatFile()
    {
        var userId = Guid.NewGuid();
        var oldFile = await Save(userId, PdfBytes, "cu.pdf");
        var newFile = await Save(userId, PdfBytes, "moi.pdf");

        await _storage.DeleteDraftFileAsync(userId, oldFile.RelativePath);

        Assert.False(File.Exists(FullPath(oldFile.RelativePath)));
        Assert.True(File.Exists(FullPath(newFile.RelativePath)));
    }

    [Fact]
    public async Task DeleteFile_CannotTouchAnotherUsersFile()
    {
        var owner = Guid.NewGuid();
        var attacker = Guid.NewGuid();
        var draft = await Save(owner, PdfBytes, "a.pdf");
        var official = await _storage.CommitDraftTranscriptAsync(owner, draft.RelativePath);

        // Đường dẫn trỏ sang thư mục người khác hoặc đi ngược thư mục đều chỉ lấy tên tệp trong thư mục của attacker
        await _storage.DeleteTranscriptFileAsync(attacker, official);
        await _storage.DeleteTranscriptFileAsync(attacker, $"../../transcripts/{owner}/{Path.GetFileName(official)}");

        Assert.True(File.Exists(FullPath(official!)));
    }

    [Fact]
    public async Task DeleteTranscriptFile_RemovesOldOfficialFile()
    {
        var userId = Guid.NewGuid();
        var draft = await Save(userId, PngBytes, "a.png");
        var official = await _storage.CommitDraftTranscriptAsync(userId, draft.RelativePath);
        Assert.True(File.Exists(FullPath(official!)));

        await _storage.DeleteTranscriptFileAsync(userId, official);

        Assert.False(File.Exists(FullPath(official!)));
    }

    // ───────────── Controller ─────────────

    private sealed class FakeFlowService : IProfileFlowService
    {
        public int UploadCalls { get; private set; }

        public Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(
            Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default)
        {
            UploadCalls++;
            var info = new DraftTranscriptFileDto(fileName, length, "application/pdf", DateTime.UtcNow);
            return Task.FromResult(new UploadDraftTranscriptResultDto(info, [], null, null, null, info));
        }

        public Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<ProfileFlowStateDto> PatchDraftAsync(Guid userId, System.Text.Json.Nodes.JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ClearDraftAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static ProfileFlowController NewController(IProfileFlowService service) => new(service)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"))
            }
        }
    };

    private static IFormFile FormFile(byte[] bytes, string name, string fileName, long? length = null) =>
        new FormFile(new MemoryStream(bytes), 0, length ?? bytes.Length, name, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

    [Fact]
    public async Task Upload_TwoFiles_ReturnsBadRequest_WithoutCallingService()
    {
        var service = new FakeFlowService();

        var result = await NewController(service).UploadDraftTranscript(
            [FormFile(PdfBytes, "files", "a.pdf"), FormFile(PdfBytes, "files", "b.pdf")], null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.UploadCalls);
    }

    [Fact]
    public async Task Upload_FilesAndFileFieldsTogether_ReturnsBadRequest()
    {
        var service = new FakeFlowService();

        var result = await NewController(service).UploadDraftTranscript(
            [FormFile(PdfBytes, "files", "a.pdf")], FormFile(PdfBytes, "file", "b.pdf"), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.UploadCalls);
    }

    [Fact]
    public async Task Upload_OverSizeLimit_ReturnsBadRequest_WithoutCallingService()
    {
        var service = new FakeFlowService();

        var result = await NewController(service).UploadDraftTranscript(
            null, FormFile(PdfBytes, "file", "a.pdf", ProfileFileStorageLimits.MaxFileSizeBytes + 1), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.UploadCalls);
    }

    [Fact]
    public async Task Upload_SingleFile_CallsService()
    {
        var service = new FakeFlowService();

        var result = await NewController(service).UploadDraftTranscript(null, FormFile(PdfBytes, "file", "a.pdf"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, service.UploadCalls);
    }

    [Fact]
    public void Upload_HasRateLimitAndRequestSizeLimit()
    {
        var method = typeof(ProfileFlowController).GetMethod(nameof(ProfileFlowController.UploadDraftTranscript))!;

        Assert.Equal(TranscriptUploadRateLimit.PolicyName, method.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        Assert.NotNull(method.GetCustomAttribute<RequestSizeLimitAttribute>());
        Assert.NotNull(method.GetCustomAttribute<RequestFormLimitsAttribute>());
        Assert.NotNull(typeof(ProfileFlowController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RateLimiter_AllowsOneAtATime_AndAtMostFivePerWindow()
    {
        using var limiter = new TranscriptUploadRateLimit.PerUserUploadLimiter();

        // Đang có 1 request chạy thì request thứ 2 bị chặn, và không bị trừ lượt
        using (var running = limiter.AttemptAcquire())
        {
            Assert.True(running.IsAcquired);
            using var parallel = limiter.AttemptAcquire();
            Assert.False(parallel.IsAcquired);
        }

        for (var i = 1; i < TranscriptUploadRateLimit.PermitsPerWindow; i++)
        {
            using var lease = limiter.AttemptAcquire();
            Assert.True(lease.IsAcquired);
        }

        using var overLimit = limiter.AttemptAcquire();
        Assert.False(overLimit.IsAcquired);
    }

    private sealed class TestHostEnvironment(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
