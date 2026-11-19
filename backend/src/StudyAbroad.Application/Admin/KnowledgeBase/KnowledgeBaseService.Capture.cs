using StudyAbroad.Application.Admin.SlmSamples;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.KnowledgeBase;

/// <summary>UC-53: câu chat hệ thống chưa trả lời được vào kho tri thức ở dạng nháp.</summary>
public partial class KnowledgeBaseService
{
    public async Task<bool> CaptureUnansweredAsync(string? question, CancellationToken ct = default)
    {
        var text = CleanLine(question);
        if (text is null || text.Length > MaxQuestionLength || SlmPersonalDataCheck.HasContactOrLink(text))
            return false;
        // ponytail: kiểm tra rồi mới thêm, hai câu giống hệt cùng lúc có thể cùng lọt; thêm unique index nếu thực tế gặp
        if ((await repository.SameQuestionAsync(text, includeDeleted: true, ct)).Count > 0)
            return false;

        var entry = new KnowledgeBaseEntry
        {
            Question = text,
            Origin = KnowledgeOrigins.ChatUnanswered,
            ReviewStatus = KnowledgeReviewStatuses.Draft,
        };
        await repository.AddAsync(
            entry, History(KnowledgeHistoryActions.Capture, null, null, KnowledgeReviewStatuses.Draft, entry.Id), ct);
        return true;
    }
}