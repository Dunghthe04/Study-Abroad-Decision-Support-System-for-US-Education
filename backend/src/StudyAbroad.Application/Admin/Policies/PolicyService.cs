using System.Globalization;
using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Policies;

public interface IPolicyService
{
    /// <summary>
    /// Tạo bản nháp mới của một loại tài liệu bằng cách CHÉP toàn văn bản đang hiệu lực (chưa có bản nào thì để trống).
    /// Loại này đã có bản nháp thì trả lại bản nháp đó (Created = false), không tạo thêm. Kind sai ném ArgumentException.
    /// </summary>
    Task<PolicyDraftResult> CreateDraftAsync(PolicyActor actor, string kind, CancellationToken ct = default);

    /// <summary>
    /// Lưu nội dung vào bản nháp. Bản đã công bố hoặc đã hết hiệu lực KHÔNG sửa được (PolicyRuleException policy_locked): muốn đổi thì tạo bản mới.
    /// Null nếu không có bản đó. Tiêu đề trống hoặc dài quá thì ném ArgumentException.
    /// </summary>
    Task<PolicyDocumentDto?> UpdateDraftAsync(PolicyActor actor, Guid id, UpdatePolicyDraftRequest request, CancellationToken ct = default);

    /// <summary>
    /// Công bố bản nháp: đặt số phiên bản (yyyy-MM, thêm -02, -03... nếu tháng đó đã có bản), khóa bản này, chuyển bản đang hiệu lực
    /// thành hết hiệu lực, ghi nhật ký; tất cả trong MỘT giao dịch. Thiếu tiêu đề, toàn văn hoặc tóm tắt thay đổi thì ném ArgumentException
    /// và không đổi gì. Không phải bản nháp thì policy_locked. Null nếu không có bản đó.
    /// </summary>
    Task<PolicyDocumentDto?> PublishAsync(PolicyActor actor, Guid id, CancellationToken ct = default);

    /// <summary>Bỏ bản nháp (xóa hẳn). Chỉ bản nháp; bản đã công bố thì policy_locked. False nếu không có bản đó.</summary>
    Task<bool> DiscardDraftAsync(PolicyActor actor, Guid id, CancellationToken ct = default);

    /// <summary>Một phiên bản kèm toàn văn; null nếu không có.</summary>
    Task<PolicyDocumentDto?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Lịch sử các phiên bản (không kèm toàn văn), mới nhất trước. kind null là mọi loại; kind sai ném ArgumentException.</summary>
    Task<IReadOnlyList<PolicyDocumentSummaryDto>> ListAsync(string? kind, CancellationToken ct = default);
}

public class PolicyService(
    IPolicyDocumentRepository documents, IAuditLogRepository audits, IUnitOfWork unitOfWork, TimeProvider? clock = null) : IPolicyService
{
    public const string CreateDraftAction = "policy.create_draft";
    public const string UpdateDraftAction = "policy.update_draft";
    public const string DiscardDraftAction = "policy.discard_draft";
    public const string PublishAction = "policy.publish";
    public const string EntityType = "policy_document";

    public const int MaxTitleLength = 200;
    public const int MaxChangeSummaryLength = 1000;
    public const int MaxContentLength = 200_000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string> DefaultTitles = new()
    {
        [PolicyKinds.TermsOfUse] = "Điều khoản sử dụng",
        [PolicyKinds.PrivacyPolicy] = "Chính sách bảo mật & Quyền riêng tư",
        [PolicyKinds.ChildDataConsent] = "Đồng ý xử lý dữ liệu cá nhân của con dưới 16 tuổi",
        [PolicyKinds.ForumRules] = "Nội quy diễn đàn",
    };

    private DateTime Now => (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public async Task<PolicyDraftResult> CreateDraftAsync(PolicyActor actor, string kind, CancellationToken ct = default)
    {
        RequireKind(kind);
        if (await documents.FindDraftAsync(kind, ct) is { } existing)
            return new PolicyDraftResult(PolicyMapper.ToDto(existing), false);

        var published = await documents.FindPublishedAsync(kind, ct);
        var draft = new PolicyDocument
        {
            Kind = kind,
            Status = PolicyStatuses.Draft,
            Title = published?.Title ?? DefaultTitles[kind],
            Content = published?.Content ?? string.Empty,
            RequiresReconsent = false,
            BasedOnId = published?.Id,
            CreatedByUserId = actor.UserId,
            CreatedAt = Now
        };

        await unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            await documents.AddAsync(draft, c);
            await audits.AddAsync(Audit(actor, CreateDraftAction, draft, new { basedOn = published?.Version }), c);
        }, ct);
        return new PolicyDraftResult(PolicyMapper.ToDto(draft), true);
    }

    public async Task<PolicyDocumentDto?> UpdateDraftAsync(PolicyActor actor, Guid id, UpdatePolicyDraftRequest request, CancellationToken ct = default)
    {
        if (await documents.FindAsync(id, ct) is not { } doc) return null;
        RequireDraft(doc);

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0) throw new ArgumentException("Tiêu đề không được để trống.");
        if (title.Length > MaxTitleLength) throw new ArgumentException($"Tiêu đề tối đa {MaxTitleLength} ký tự.");
        var content = request.Content ?? string.Empty;
        if (content.Length > MaxContentLength) throw new ArgumentException($"Nội dung tối đa {MaxContentLength} ký tự.");
        var summary = string.IsNullOrWhiteSpace(request.ChangeSummary) ? null : request.ChangeSummary.Trim();
        if (summary is { Length: > MaxChangeSummaryLength }) throw new ArgumentException($"Tóm tắt thay đổi tối đa {MaxChangeSummaryLength} ký tự.");

        doc.Title = title;
        doc.Content = content;
        doc.ChangeSummary = summary;
        doc.RequiresReconsent = request.RequiresReconsent;
        doc.UpdatedAt = Now;

        await unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            await documents.SaveAsync(c);
            await audits.AddAsync(Audit(actor, UpdateDraftAction, doc, new { requiresReconsent = doc.RequiresReconsent, contentLength = doc.Content.Length }), c);
        }, ct);
        return PolicyMapper.ToDto(doc);
    }

    public async Task<PolicyDocumentDto?> PublishAsync(PolicyActor actor, Guid id, CancellationToken ct = default)
    {
        if (await documents.FindAsync(id, ct) is not { } draft) return null;
        RequireDraft(draft);

        if (string.IsNullOrWhiteSpace(draft.Title)) throw new ArgumentException("Cần có tiêu đề trước khi công bố.");
        if (string.IsNullOrWhiteSpace(draft.Content)) throw new ArgumentException("Cần có nội dung trước khi công bố.");
        if (string.IsNullOrWhiteSpace(draft.ChangeSummary)) throw new ArgumentException("Cần nhập tóm tắt thay đổi trước khi công bố.");

        var now = Now;
        var previous = await documents.FindPublishedAsync(draft.Kind, ct);
        var version = NextVersion(now, await documents.ListVersionsAsync(draft.Kind, now.ToString("yyyy-MM", CultureInfo.InvariantCulture), ct));

        // Bản cũ hết hiệu lực TRƯỚC, bản mới có hiệu lực SAU, cùng một giao dịch: DB cho phép tối đa một bản đang hiệu lực mỗi loại.
        await unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            if (previous is not null)
            {
                previous.Status = PolicyStatuses.Retired;
                previous.RetiredAt = now;
                previous.UpdatedAt = now;
                await documents.SaveAsync(c);
            }
            draft.Version = version;
            draft.Status = PolicyStatuses.Published;
            draft.PublishedAt = now;
            draft.PublishedByUserId = actor.UserId;
            draft.UpdatedAt = now;
            await documents.SaveAsync(c);
            await audits.AddAsync(Audit(actor, PublishAction, draft, new
            {
                requiresReconsent = draft.RequiresReconsent, previousVersion = previous?.Version, changeSummary = draft.ChangeSummary
            }), c);
        }, ct);
        return PolicyMapper.ToDto(draft);
    }

    public async Task<bool> DiscardDraftAsync(PolicyActor actor, Guid id, CancellationToken ct = default)
    {
        if (await documents.FindAsync(id, ct) is not { } doc) return false;
        RequireDraft(doc);

        await unitOfWork.ExecuteInTransactionAsync(async c =>
        {
            await documents.RemoveAsync(doc, c);
            await audits.AddAsync(Audit(actor, DiscardDraftAction, doc, new { }), c);
        }, ct);
        return true;
    }

    public async Task<PolicyDocumentDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        await documents.FindAsync(id, ct) is { } doc ? PolicyMapper.ToDto(doc) : null;

    public async Task<IReadOnlyList<PolicyDocumentSummaryDto>> ListAsync(string? kind, CancellationToken ct = default)
    {
        if (kind is not null) RequireKind(kind);
        return (await documents.ListAsync(kind, ct)).Select(PolicyMapper.ToSummary).ToList();
    }

    // ---------------------------------------------------------------- quy tắc nhỏ

    // Số phiên bản: yyyy-MM; tháng đó đã có bản thì thêm -02, -03... Thứ tự chữ cũng là thứ tự thời gian.
    public static string NextVersion(DateTime nowUtc, IReadOnlyCollection<string> usedWithPrefix)
    {
        var prefix = nowUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        if (!usedWithPrefix.Contains(prefix, StringComparer.Ordinal)) return prefix;
        var highest = usedWithPrefix
            .Select(v => v.StartsWith(prefix + "-", StringComparison.Ordinal) && int.TryParse(v[(prefix.Length + 1)..], out var n) ? n : 1)
            .DefaultIfEmpty(1).Max();
        return $"{prefix}-{highest + 1:00}";
    }

    private static void RequireKind(string kind)
    {
        if (!PolicyKinds.All.Contains(kind, StringComparer.Ordinal))
            throw new ArgumentException($"Loại tài liệu không hợp lệ: {kind}.");
    }

    // Chỉ bản nháp được sửa, công bố, bỏ. Bản đã công bố là bằng chứng người dùng đã đồng ý đúng văn bản đó.
    private static void RequireDraft(PolicyDocument doc)
    {
        if (doc.Status != PolicyStatuses.Draft)
            throw new PolicyRuleException("policy_locked", "Bản đã công bố không sửa được. Muốn đổi nội dung, hãy tạo bản mới.");
    }

    private static AuditLog Audit(PolicyActor actor, string action, PolicyDocument doc, object detail) => new()
    {
        ActorUserId = actor.UserId,
        Action = action,
        EntityType = EntityType,
        EntityId = doc.Id.ToString(),
        // Nhật ký chỉ có loại, số phiên bản và các cờ, không chép toàn văn
        DetailJson = JsonSerializer.Serialize(new { kind = doc.Kind, version = doc.Version, detail }, Json),
        IpAddress = actor.IpAddress,
        UserAgent = actor.UserAgent
    };
}
