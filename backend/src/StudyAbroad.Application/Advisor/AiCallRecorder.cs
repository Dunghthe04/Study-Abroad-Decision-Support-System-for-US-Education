using Microsoft.Extensions.Logging;

namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Ghi các lần gọi AI vào ai_calls: mỗi số đo một dòng (AiCallMapper), nối với tin trả lời nếu có.
/// Dùng chung cho lượt chat (có chat_message_id) và các luồng ngoài chat như OCR bảng điểm, bậc ngoại khóa (chat_message_id trống).
/// Lỗi khi ghi chỉ cảnh báo, không bao giờ làm hỏng việc chính của người dùng. Không có số đo thì không làm gì.
/// </summary>
public class AiCallRecorder(IAiCallRepository repository, ILogger? logger = null)
{
    public async Task RecordAsync(Guid userId, Guid? chatMessageId, IReadOnlyList<AiUsageDto>? usage, CancellationToken ct)
    {
        if (usage is not { Count: > 0 })
            return;
        try
        {
            await repository.AddRangeAsync(usage.Select(call => AiCallMapper.ToEntity(call, userId, chatMessageId)).ToList(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger?.LogWarning(ex, "Không ghi được nhật ký gọi AI của người dùng {UserId}; việc chính vẫn hoàn thành bình thường", userId);
        }
    }
}
