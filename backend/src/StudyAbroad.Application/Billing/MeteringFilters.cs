using System.Linq.Expressions;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Lần gọi AI nào bị tính vào hạn mức: chỉ Claude (SLM, embedding, Ollama miễn phí), chỉ lượt chat đã đóng dấu gói.
/// plan_code chỉ được đóng dấu trên lượt chat, nên OCR, ngoại khóa và lượt của nhân viên (plan_code null) không bị tính.
/// Không lọc theo chat_message_id: tắt lưu tin (Chat:StoreMessages) hay lưu tin lỗi thì cột đó trống.
/// Dùng chung cho repository (EF dịch ra SQL) và test.
/// </summary>
public static class MeteringFilters
{
    public static Expression<Func<AiCall, bool>> Cycle(Guid cycleId) =>
        a => a.UserSubscriptionId == cycleId && a.Kind == BillingRules.MeteredAiKind;

    /// <summary>Lượt chat dùng hạn mức Free của người này từ fromUtc: không có kỳ nhưng có plan_code.</summary>
    public static Expression<Func<AiCall, bool>> Free(Guid userId, DateTime fromUtc) =>
        a => a.UserId == userId && a.UserSubscriptionId == null && a.PlanCode != null
            && a.Kind == BillingRules.MeteredAiKind && a.CreatedAt >= fromUtc;
}
