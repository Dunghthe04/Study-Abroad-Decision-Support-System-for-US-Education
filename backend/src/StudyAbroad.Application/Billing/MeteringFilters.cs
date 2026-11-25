using System.Linq.Expressions;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing;

/// <summary>
/// Lần gọi AI nào bị tính vào hạn mức: cả LLM (claude) và SLM (ollama) của lượt chat đã đóng dấu gói (embedding, OCR, ngoại khóa không tính).
/// plan_code chỉ được đóng dấu trên lượt chat, nên OCR, ngoại khóa và lượt của nhân viên (plan_code null) không bị tính.
/// Hạn mức Free lọc đúng plan_code = free: kỳ bị xóa (FK set null) để lại plan_code của gói trả phí, không được tính sang Free.
/// Không lọc theo chat_message_id: tắt lưu tin (Chat:StoreMessages) hay lưu tin lỗi thì cột đó trống.
/// Dùng chung cho repository (EF dịch ra SQL) và test.
/// </summary>
public static class MeteringFilters
{
    public static Expression<Func<AiCall, bool>> Cycle(Guid cycleId, DateTime? fromUtc = null, DateTime? toUtc = null) =>
        fromUtc is { } from && toUtc is { } to
            ? a => a.UserSubscriptionId == cycleId
                && (a.Kind == BillingRules.ClaudeAiKind || a.Kind == BillingRules.OllamaAiKind)
                && a.CreatedAt >= from && a.CreatedAt < to
            : a => a.UserSubscriptionId == cycleId
                && (a.Kind == BillingRules.ClaudeAiKind || a.Kind == BillingRules.OllamaAiKind);

    /// <summary>Lượt chat dùng hạn mức Free của người này từ fromUtc: không có kỳ, plan_code = free.</summary>
    public static Expression<Func<AiCall, bool>> Free(Guid userId, DateTime fromUtc) =>
        a => a.UserId == userId && a.UserSubscriptionId == null && a.PlanCode == PlanKinds.Free
            && (a.Kind == BillingRules.ClaudeAiKind || a.Kind == BillingRules.OllamaAiKind)
            && a.CreatedAt >= fromUtc;
}
