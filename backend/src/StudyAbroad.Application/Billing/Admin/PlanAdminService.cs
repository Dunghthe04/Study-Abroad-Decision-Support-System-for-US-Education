using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Billing.Admin;

/// <summary>
/// Quản lý gói (UC-79). Giá, thời hạn, hạn mức đổi ở đây chỉ ảnh hưởng đơn và kỳ mới: đơn và kỳ đã có giữ bản chụp riêng,
/// nên mọi thao tác ghi chỉ đụng đúng một dòng subscription_plans (cùng một dòng audit, cùng transaction).
/// </summary>
public sealed partial class PlanAdminService(IPlanAdminRepository plans, IAuditLogRepository auditLogs, IUnitOfWork unitOfWork) : IPlanAdminService
{
    private const int MaxCodeLength = 64;
    private const int MaxNameLength = 128;
    private const int MaxDescriptionLength = 1000;
    /// <summary>Cột price_vnd là numeric(12,2): tối đa 10 chữ số phần nguyên.</summary>
    private const long MaxPriceVnd = 9_999_999_999;
    private static readonly int[] PaidDurations = [30, 90];

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex CodePattern();

    public async Task<IReadOnlyList<AdminPlanDto>> ListAsync(CancellationToken ct = default) =>
        (await plans.ListAsync(ct)).Select(r => AdminPlanDto.From(r.Plan, r.Usage)).ToList();

    public async Task<AdminPlanDto> CreateAsync(Guid adminUserId, SavePlanRequest request, CancellationToken ct = default)
    {
        var code = request.Code?.Trim() ?? "";
        if (code.Length is 0 or > MaxCodeLength || !CodePattern().IsMatch(code))
            throw Rule("invalid_code", 400, "Mã gói chỉ gồm chữ thường, số và dấu gạch ngang, tối đa 64 ký tự.");
        var kind = request.Kind?.Trim() ?? "";
        if (!PlanKinds.All.Contains(kind))
            throw Rule("invalid_kind", 400, "Loại gói phải là free, personal hoặc family.");
        // ponytail: kiểm tra trùng trước khi ghi, không khóa; hai admin tạo trùng mã cùng lúc sẽ vấp index duy nhất (500), hiếm tới mức bỏ qua.
        if (await plans.CodeExistsAsync(code, ct))
            throw Rule("code_taken", 409, "Mã gói này đã tồn tại.");
        if (kind == PlanKinds.Free && await plans.KindExistsAsync(PlanKinds.Free, ct))
            throw Rule("free_plan_exists", 409, "Đã có gói Miễn phí, không tạo thêm được.");
        var fields = ValidateFields(request, kind);

        AdminPlanDto? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var plan = new SubscriptionPlan { Code = code, Kind = kind };
            fields.ApplyTo(plan);
            await plans.AddAsync(plan, txCt);
            await AuditAsync(adminUserId, "plan.create", plan, before: null, after: Snapshot(plan), txCt);
            result = AdminPlanDto.From(plan, new PlanUsage(0, 0, 0));
        }, ct);
        return result!;
    }

    public async Task<AdminPlanDto> UpdateAsync(Guid adminUserId, Guid planId, SavePlanRequest request, CancellationToken ct = default)
    {
        AdminPlanDto? result = null;
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var found = await plans.FindAsync(planId, txCt) ?? throw NotFound();
            var plan = found.Plan;
            // Kind đổi sẽ đổi làn và chỗ ngồi của kỳ đang chạy; code là khóa người dùng thấy: cả hai chỉ đặt một lần lúc tạo.
            if (!string.IsNullOrWhiteSpace(request.Kind) && request.Kind.Trim() != plan.Kind)
                throw Rule("kind_immutable", 409, "Không đổi được loại gói sau khi tạo.");
            var fields = ValidateFields(request, plan.Kind);

            var before = Snapshot(plan);
            fields.ApplyTo(plan);
            await plans.SaveAsync(txCt);
            await AuditAsync(adminUserId, "plan.update", plan, before, after: Snapshot(plan), txCt);
            result = AdminPlanDto.From(plan, found.Usage);
        }, ct);
        return result!;
    }

    public async Task DeleteAsync(Guid adminUserId, Guid planId, CancellationToken ct = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(async txCt =>
        {
            var found = await plans.FindAsync(planId, txCt) ?? throw NotFound();
            var plan = found.Plan;
            if (plan.Kind == PlanKinds.Free)
                throw FreePlanLocked();
            if (found.Usage.Cycles > 0 || found.Usage.Orders > 0)
                throw Rule("plan_in_use", 409, "Gói đã có đơn hàng hoặc chu kỳ sử dụng, không xóa được. Hãy tắt mở bán thay vì xóa.");

            var before = Snapshot(plan);
            await plans.RemoveAsync(plan, txCt);
            await AuditAsync(adminUserId, "plan.delete", plan, before, after: null, txCt);
        }, ct);
    }

    /// <summary>Kiểm tra các trường sửa được và trả giá trị đã chuẩn hóa. Kind quyết định luật gói free (giá 0, luôn bán, không thời hạn).</summary>
    private static PlanFields ValidateFields(SavePlanRequest r, string kind)
    {
        var name = r.Name?.Trim() ?? "";
        if (name.Length is 0 or > MaxNameLength)
            throw Rule("invalid_name", 400, "Tên gói phải từ 1 đến 128 ký tự.");
        var description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        if (description is { Length: > MaxDescriptionLength })
            throw Rule("invalid_value", 400, "Mô tả tối đa 1000 ký tự.");
        if (r.PriceVnd is < 0 or > MaxPriceVnd || r.TokenQuota < 0 || r.EstimatedQuestions < 0)
            throw Rule("invalid_value", 400, "Giá, hạn mức token và số câu hỏi ước lượng phải là số không âm.");

        int? durationDays = null;
        if (kind == PlanKinds.Free)
        {
            if (r.PriceVnd != 0 || !r.IsActive)
                throw FreePlanLocked();
        }
        else
        {
            if (r.DurationDays is not { } days || !PaidDurations.Contains(days))
                throw Rule("invalid_duration", 400, "Thời hạn chỉ được 30 hoặc 90 ngày.");
            durationDays = days;
        }
        return new PlanFields(name, description, r.PriceVnd, durationDays, r.TokenQuota, r.EstimatedQuestions, r.IsActive, r.DisplayOrder);
    }

    private sealed record PlanFields(
        string Name, string? Description, long PriceVnd, int? DurationDays, long TokenQuota, int EstimatedQuestions, bool IsActive, int DisplayOrder)
    {
        public void ApplyTo(SubscriptionPlan plan)
        {
            plan.Name = Name;
            plan.Description = Description;
            plan.PriceVnd = PriceVnd;
            plan.DurationDays = DurationDays;
            plan.TokenQuota = TokenQuota;
            plan.EstimatedQuestions = EstimatedQuestions;
            plan.IsActive = IsActive;
            plan.DisplayOrder = DisplayOrder;
        }
    }

    private static object Snapshot(SubscriptionPlan p) => new
    {
        code = p.Code, name = p.Name, description = p.Description, kind = p.Kind, priceVnd = (long)p.PriceVnd, durationDays = p.DurationDays,
        tokenQuota = p.TokenQuota, estimatedQuestions = p.EstimatedQuestions, isActive = p.IsActive, displayOrder = p.DisplayOrder,
    };

    private Task AuditAsync(Guid adminUserId, string action, SubscriptionPlan plan, object? before, object? after, CancellationToken ct) =>
        auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = action,
            EntityType = "subscription_plan",
            EntityId = plan.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { before, after }),
        }, ct);

    private static BillingRuleException Rule(string code, int status, string message) => new(code, status, message);
    private static BillingRuleException NotFound() => Rule("plan_not_found", 404, "Không tìm thấy gói.");
    private static BillingRuleException FreePlanLocked() =>
        Rule("free_plan_locked", 409, "Gói Miễn phí luôn giá 0đ, luôn mở bán và không xóa được.");
}
