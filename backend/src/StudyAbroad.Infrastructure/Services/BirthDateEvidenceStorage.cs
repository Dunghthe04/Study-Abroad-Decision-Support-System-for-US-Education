using System.Net;
using System.Security.Cryptography;
using System.Text;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyAbroad.Infrastructure.Auth;
using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Infrastructure.MediaUploads;

namespace StudyAbroad.Infrastructure.Services;

public sealed class BirthDateEvidenceOptions
{
    /// <summary>base64 của đúng 32 byte (biến EVIDENCE_ENCRYPTION_KEY). Trống thì dẫn xuất từ JWT_SECRET_KEY.</summary>
    public string? EncryptionKey { get; set; }
}

/// <summary>
/// [UC-107 / UC-150] Minh chứng ngày sinh lưu trên Cloudinary để mọi bản api (Azure Container Apps) cùng đọc được,
/// không lưu ổ đĩa api vì ổ đó mất khi khởi động lại.
/// Ảnh giấy tờ rất nhạy cảm nên:
/// - api mã hóa AES-256-GCM trước khi tải lên, Cloudinary chỉ giữ bản mã; khóa nằm ở biến môi trường, không ở Cloudinary hay DB;
/// - tải lên dạng raw + authenticated: không có link công khai, chỉ link ký bằng ApiSecret mới tải được;
/// - khóa lưu trữ là dữ liệu bổ sung (AAD) của GCM nên bản mã chép sang chỗ khác sẽ không giải mã được.
/// </summary>
public sealed class BirthDateEvidenceStorage : IBirthDateEvidenceStorage
{
    private const string DeliveryType = "authenticated";
    private const byte FormatVersion = 1;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly HttpClient _http;
    private readonly Cloudinary _cloudinary;
    private readonly string _prefix;
    private readonly Lazy<byte[]> _key;
    private readonly ILogger<BirthDateEvidenceStorage> _logger;

    public BirthDateEvidenceStorage(
        HttpClient http,
        IOptions<CloudinaryOptions> cloudinary,
        IOptions<BirthDateEvidenceOptions> options,
        IOptions<JwtOptions> jwt,
        ILogger<BirthDateEvidenceStorage> logger)
    {
        _http = http;
        _logger = logger;
        var settings = cloudinary.Value;
        _cloudinary = new Cloudinary(new Account(settings.CloudName, settings.ApiKey, settings.ApiSecret));
        _cloudinary.Api.Secure = true;
        _prefix = $"{settings.Folder.Trim('/')}/birth-date-evidence/";
        _key = new Lazy<byte[]>(() => DeriveKey(options.Value.EncryptionKey, jwt.Value.SecretKey, logger));
    }

    public async Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default)
    {
        // Đuôi tệp không đưa vào tên trên Cloudinary để bên ngoài không đoán được loại giấy tờ
        var publicId = $"{_prefix}{requestId:N}/{Guid.NewGuid():N}";
        var sealedBytes = Encrypt(content, publicId);
        using var stream = new MemoryStream(sealedBytes, writable: false);
        var parameters = new RawUploadParams
        {
            // Không đuôi: Cloudinary chặn .bin, còn đuôi khác bị nối vào public id của tệp raw
            File = new FileDescription("evidence", stream),
            PublicId = publicId,
            Type = DeliveryType,
            Overwrite = false,
        };

        RawUploadResult result;
        try
        {
            result = await _cloudinary.UploadAsync(parameters, "raw", ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không tải được minh chứng ngày sinh lên Cloudinary");
            throw new InvalidOperationException("Không lưu được tệp minh chứng, vui lòng thử lại.");
        }

        if (result.Error is not null || (int)result.StatusCode is < 200 or >= 300 || result.PublicId != publicId)
        {
            _logger.LogError("Cloudinary trả lỗi khi lưu minh chứng ngày sinh: {Status} {Error}", result.StatusCode, result.Error?.Message);
            throw new InvalidOperationException("Không lưu được tệp minh chứng, vui lòng thử lại.");
        }
        return publicId;
    }

    public async Task<Stream?> OpenAsync(string path, CancellationToken ct = default)
    {
        if (!IsOwnPath(path))
            return null;
        var url = _cloudinary.Api.Url.ResourceType("raw").Type(DeliveryType).Signed(true).Secure(true).BuildUrl(path);
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Cloudinary trả {Status} khi đọc minh chứng ngày sinh", (int)response.StatusCode);
            throw new InvalidOperationException("Chưa đọc được tệp minh chứng, vui lòng thử lại.");
        }

        var sealedBytes = await response.Content.ReadAsByteArrayAsync(ct);
        var plain = Decrypt(sealedBytes, path);
        if (plain is null)
        {
            _logger.LogError("Minh chứng ngày sinh {Path} không giải mã được (sai khóa hoặc bị sửa)", path);
            return null;
        }
        return new MemoryStream(plain, writable: false);
    }

    public async Task DeleteAsync(string? path, CancellationToken ct = default)
    {
        if (path is null || !IsOwnPath(path))
            return;
        try
        {
            // CloudinaryDotNet 1.29.3 DestroyAsync không nhận CancellationToken
            var result = await _cloudinary.DestroyAsync(new DeletionParams(path)
            {
                ResourceType = ResourceType.Raw,
                Type = DeliveryType,
                Invalidate = true,
            });
            if (result.Error is not null)
                _logger.LogWarning("Cloudinary chưa xóa được minh chứng ngày sinh {Path}: {Error}", path, result.Error.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Yêu cầu vẫn đóng, đường dẫn trong DB đã về null; bản trên Cloudinary chỉ là bản mã
            _logger.LogWarning(ex, "Không xóa được minh chứng ngày sinh {Path} trên Cloudinary", path);
        }
    }

    /// <summary>Chỉ nhận khóa nằm trong thư mục minh chứng, chặn ../ và khóa trỏ sang tệp khác trên Cloudinary.</summary>
    private bool IsOwnPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith(_prefix, StringComparison.Ordinal)
        && !path.Contains("..", StringComparison.Ordinal)
        && !path.Contains('\\')
        && path[_prefix.Length..].Split('/') is [{ Length: 32 } a, { Length: 32 } b]
        && a.All(char.IsAsciiHexDigitLower) && b.All(char.IsAsciiHexDigitLower);

    public byte[] Encrypt(byte[] plain, string path)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plain.Length];
        var tag = new byte[TagSize];
        using (var aes = new AesGcm(_key.Value, TagSize))
            aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(path));
        return [FormatVersion, .. nonce, .. cipher, .. tag];
    }

    /// <summary>Null khi bản mã bị sửa, hỏng, mã bằng khóa khác hoặc chép từ khóa lưu trữ khác.</summary>
    public byte[]? Decrypt(byte[] data, string path)
    {
        if (data.Length <= 1 + NonceSize + TagSize || data[0] != FormatVersion)
            return null;
        var nonce = data.AsSpan(1, NonceSize);
        var cipher = data.AsSpan(1 + NonceSize, data.Length - 1 - NonceSize - TagSize);
        var tag = data.AsSpan(data.Length - TagSize);
        var plain = new byte[cipher.Length];
        try
        {
            using var aes = new AesGcm(_key.Value, TagSize);
            aes.Decrypt(nonce, cipher, tag, plain, Encoding.UTF8.GetBytes(path));
            return plain;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static byte[] DeriveKey(string? configured, string? jwtSecret, ILogger logger)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var key = Convert.FromBase64String(configured.Trim());
            if (key.Length != 32)
                throw new InvalidOperationException("EVIDENCE_ENCRYPTION_KEY phải là base64 của đúng 32 byte (openssl rand -base64 32).");
            return key;
        }
        if (string.IsNullOrWhiteSpace(jwtSecret))
            throw new InvalidOperationException("Thiếu EVIDENCE_ENCRYPTION_KEY và JWT_SECRET_KEY để mã hóa minh chứng ngày sinh.");
        logger.LogWarning("EVIDENCE_ENCRYPTION_KEY chưa đặt, đang dẫn xuất khóa mã hóa minh chứng từ JWT_SECRET_KEY. Nên đặt biến riêng ở Production.");
        // Nhãn riêng nên ra khóa khác khóa TOTP và khóa hàng đợi mail
        return HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(jwtSecret), 32,
            info: Encoding.UTF8.GetBytes("usas-birth-date-evidence-encryption"));
    }
}
