using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.MediaUploads;
using StudyAbroad.Application.StudyCenters;

namespace StudyAbroad.Infrastructure.MediaUploads;

/// <summary>
/// [UC-93] Xóa ảnh và tài liệu của trung tâm trên Cloudinary. IMediaUploader được lấy lười như MediaController:
/// khi Cloudinary chưa cấu hình, lấy nó ném OptionsValidationException và không được làm hỏng cả luồng xóa trung tâm.
/// </summary>
public sealed class CenterMediaCleaner(IServiceProvider services) : ICenterMediaCleaner
{
    public async Task<IReadOnlyList<string>> DeleteAsync(IReadOnlyCollection<string> urls, CancellationToken ct = default)
    {
        var distinct = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.Ordinal).ToList();
        if (distinct.Count == 0) return [];

        IMediaUploader uploader;
        try
        {
            uploader = services.GetRequiredService<IMediaUploader>();
        }
        catch (OptionsValidationException)
        {
            // Chưa cấu hình Cloudinary thì không thể có tệp nào trên đó từ môi trường này; chỉ báo các URL trông như của Cloudinary
            return distinct.Where(IsCloudinaryUrl).ToList();
        }

        var failed = new List<string>();
        foreach (var url in distinct)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // false = URL không thuộc Cloudinary của hệ thống hoặc tệp đã không còn: không phải lỗi
                await uploader.DeleteAsync(url, cancellationToken: ct);
            }
            catch (ArgumentException)
            {
                // Chuỗi không phải URL hay public id hợp lệ (ví dụ liên kết ngoài do trung tâm tự nhập): không có gì để xóa trên CDN
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add(url);
            }
        }

        return failed;
    }

    private static bool IsCloudinaryUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Host.EndsWith("cloudinary.com", StringComparison.OrdinalIgnoreCase);
}
