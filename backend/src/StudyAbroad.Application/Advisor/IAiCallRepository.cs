using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Ghi nhật ký các lần gọi AI vào bảng ai_calls để admin xem token, tốc độ, lỗi. Chỉ chứa số đo, không có nội dung chat
/// nên không bị công tắc Chat:StoreMessages chi phối. Lỗi thì ném, bên gọi quyết định.
/// </summary>
public interface IAiCallRepository
{
    Task AddRangeAsync(IReadOnlyList<AiCall> calls, CancellationToken ct = default);
}
