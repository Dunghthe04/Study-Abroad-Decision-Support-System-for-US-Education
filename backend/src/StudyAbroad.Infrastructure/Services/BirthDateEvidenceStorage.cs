using Microsoft.Extensions.Hosting;
using StudyAbroad.Application.BirthDateChanges;

namespace StudyAbroad.Infrastructure.Services;

/// <summary>
/// [UC-107 / UC-150] Tệp minh chứng ngày sinh ở uploads/birth-date-evidence/{requestId}/ như tài liệu xác minh trung tâm.
/// Thư mục này không phục vụ qua static files; Admin chỉ tải qua API có ghi nhật ký truy cập.
/// </summary>
public class BirthDateEvidenceStorage(IHostEnvironment env) : IBirthDateEvidenceStorage
{
    private const string Folder = "birth-date-evidence";
    private readonly string _uploadsRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, "uploads"));

    public async Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default)
    {
        var relative = Path.Combine(Folder, requestId.ToString("N"), $"{Guid.NewGuid():N}{extension}");
        var full = Path.Combine(_uploadsRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllBytesAsync(full, content, ct);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Stream? Open(string path)
    {
        var full = Resolve(path);
        return full is not null && File.Exists(full) ? File.OpenRead(full) : null;
    }

    public void Delete(string? path)
    {
        if (Resolve(path) is not { } full)
            return;
        try
        {
            if (File.Exists(full))
                File.Delete(full);
            var dir = Path.GetDirectoryName(full);
            if (dir is not null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch (IOException)
        {
            // Tệp đang bị mở hoặc đã mất: yêu cầu vẫn đóng, đường dẫn trong DB đã về null
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Chỉ nhận đường dẫn nằm trong uploads/birth-date-evidence, chặn đường dẫn kiểu ../</summary>
    private string? Resolve(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var full = Path.GetFullPath(Path.Combine(_uploadsRoot, path));
        var allowedRoot = Path.Combine(_uploadsRoot, Folder) + Path.DirectorySeparatorChar;
        return full.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
