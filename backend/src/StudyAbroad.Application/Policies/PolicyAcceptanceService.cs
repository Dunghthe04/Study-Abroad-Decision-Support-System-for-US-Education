using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Application.Admin.Policies;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Policies;

public interface IPolicyAcceptanceService
{
    /// <summary>Người dùng hiện tại đã đồng ý những bản nào, còn bản nào phải đồng ý. Vai trò nội bộ (admin, moderator) luôn không phải đồng ý.</summary>
    Task<PolicyAcceptanceStatusDto> GetStatusAsync(Guid userId, string role, CancellationToken ct = default);

    /// <summary>
    /// Ghi đồng ý theo ĐÚNG phiên bản người dùng đã đọc. Tất cả hoặc không gì cả: một mục sai thì không ghi mục nào.
    /// Sai dữ liệu gửi lên ném ArgumentException; phiên bản đã đổi (admin vừa công bố bản mới) ném PolicyRuleException("policy_changed").
    /// Trả về tình trạng mới sau khi ghi.
    /// </summary>
    Task<PolicyAcceptanceStatusDto> AcceptAsync(Guid userId, string role, AcceptPoliciesRequest request, string? ipAddress, CancellationToken ct = default);

    /// <summary>
    /// Người dùng này có đang bị chặn vì chưa đồng ý bản bắt buộc không. Chạy ở MỌI lệnh gọi API cần đăng nhập nên rẻ:
    /// kết quả "ổn" được nhớ 60 giây, kết quả "bị chặn" không bao giờ được nhớ (đồng ý xong là hết chặn ngay).
    /// </summary>
    Task<bool> MustAcceptAsync(Guid userId, string role, CancellationToken ct = default);
}

/// <summary>
/// [UC-104] Xác nhận chính sách và điều khoản của tài khoản. Luật "có phải đồng ý không" nằm ở <see cref="PolicyAcceptanceRules"/>;
/// lớp này chỉ gom dữ liệu (các bản đã công bố, lần đồng ý gần nhất) rồi ghi bằng chứng.
/// </summary>
public class PolicyAcceptanceService(
    IPublishedPolicyReader policies, IConsentRepository consents, IUnitOfWork unitOfWork,
    // Nhớ kết quả "ổn" để không truy vấn DB ở mọi lệnh gọi API; null (test cũ) thì luôn truy vấn
    IMemoryCache? cache = null) : IPolicyAcceptanceService
{
    /// <summary>
    /// Người đã ổn được nhớ bấy nhiêu giây. Đây cũng là độ trễ tối đa để một bản bắt buộc MỚI công bố bắt đầu chặn người
    /// đang dùng (người đăng nhập mới thì bị chặn ngay vì chưa có kết quả nhớ).
    /// </summary>
    public const int OkCacheSeconds = 60;

    private static string OkKey(Guid userId) => $"policy-acceptance-ok:{userId}";

    public const string PolicyChangedCode = "policy_changed";
    public const string PolicyChangedMessage = "Nội dung vừa được cập nhật. Vui lòng tải lại trang và đọc lại trước khi đồng ý.";

    /// <summary>Vai trò phải đồng ý. Admin và moderator là người nội bộ nên không bị chặn.</summary>
    public static bool AppliesTo(string role) =>
        role is UserRoles.Student or UserRoles.Parent or UserRoles.Center;

    public async Task<bool> MustAcceptAsync(Guid userId, string role, CancellationToken ct = default)
    {
        if (!AppliesTo(role)) return false;
        if (cache is not null && cache.TryGetValue(OkKey(userId), out _)) return false;

        var must = (await GetStatusAsync(userId, role, ct)).MustAccept;
        if (!must) cache?.Set(OkKey(userId), true, TimeSpan.FromSeconds(OkCacheSeconds));
        return must;
    }

    public async Task<PolicyAcceptanceStatusDto> GetStatusAsync(Guid userId, string role, CancellationToken ct = default)
    {
        if (!AppliesTo(role))
            return new PolicyAcceptanceStatusDto(MustAccept: false, HasUpdates: false, Items: []);

        var items = new List<PolicyAcceptanceItemDto>();
        foreach (var kind in PolicyAcceptanceRules.AccountKinds)
            items.Add(await BuildItemAsync(userId, kind, ct));

        return new PolicyAcceptanceStatusDto(
            MustAccept: items.Any(i => i.Blocks),
            HasUpdates: items.Any(i => i.State is "update_optional" or "update_required"),
            Items: items);
    }

    public async Task<PolicyAcceptanceStatusDto> AcceptAsync(
        Guid userId, string role, AcceptPoliciesRequest request, string? ipAddress, CancellationToken ct = default)
    {
        if (!AppliesTo(role))
            throw new ArgumentException("Loại tài khoản này không cần xác nhận điều khoản.");

        var requested = request.Items;
        if (requested is null || requested.Count == 0)
            throw new ArgumentException("Chưa chọn tài liệu nào để đồng ý.");

        // Kiểm tra HẾT trước khi ghi: một mục sai thì không ghi mục nào
        var versionsToWrite = new List<(string Kind, string Version)>();
        foreach (var item in requested)
        {
            if (!PolicyAcceptanceRules.AccountKinds.Contains(item.Kind, StringComparer.Ordinal))
                throw new ArgumentException($"Loại tài liệu '{item.Kind}' không có trong danh sách cần đồng ý.");
            if (versionsToWrite.Any(v => v.Kind == item.Kind))
                throw new ArgumentException($"Loại tài liệu '{item.Kind}' bị gửi hai lần.");
            if (string.IsNullOrWhiteSpace(item.Version))
                throw new ArgumentException("Thiếu phiên bản đã đọc.");

            // Phiên bản người dùng đọc phải là bản đang hiệu lực: nếu admin vừa công bố bản mới thì bằng chứng sẽ không khớp văn bản họ đã đọc
            var published = await policies.ListPublishedAsync(item.Kind, ct);
            var current = published.Select(p => p.Version).Max(StringComparer.Ordinal);
            if (current is null || !string.Equals(current, item.Version, StringComparison.Ordinal))
                throw new PolicyRuleException(PolicyChangedCode, PolicyChangedMessage);

            versionsToWrite.Add((item.Kind, item.Version));
        }

        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            foreach (var (kind, version) in versionsToWrite)
            {
                // Bấm hai lần hoặc hai tab: đã đồng ý đúng bản này rồi thì không thêm dòng trùng
                var latest = await consents.GetLatestAsync(userId, kind, token);
                if (latest is { Granted: true } && latest.PolicyVersion == version)
                    continue;

                await consents.AddAsync(new Consent
                {
                    UserId = userId,
                    Purpose = kind,
                    Granted = true,
                    PolicyVersion = version,
                    GrantedIp = ipAddress,
                }, token);
            }
        }, ct);

        cache?.Remove(OkKey(userId));   // vừa đồng ý: lần kiểm tra sau tính lại từ dữ liệu mới
        return await GetStatusAsync(userId, role, ct);
    }

    private async Task<PolicyAcceptanceItemDto> BuildItemAsync(Guid userId, string kind, CancellationToken ct)
    {
        var published = await policies.ListPublishedAsync(kind, ct);
        var latest = await consents.GetLatestAsync(userId, kind, ct);
        // Lần gần nhất là từ chối thì tính như chưa đồng ý
        var accepted = latest is { Granted: true } ? latest.PolicyVersion : null;

        var versions = published.Select(p => new PublishedPolicyVersion(p.Version, p.RequiresReconsent)).ToList();
        var state = PolicyAcceptanceRules.Evaluate(versions, accepted);
        var current = published.OrderByDescending(p => p.Version, StringComparer.Ordinal).FirstOrDefault();

        return new PolicyAcceptanceItemDto(
            kind, StateCode(state), PolicyAcceptanceRules.Blocks(state),
            current?.Version, current?.Title, current?.ChangeSummary,
            // Chỉ báo phiên bản đã đồng ý khi nó khớp một bản đã công bố (dữ liệu lạ như "v1.0" thì coi như chưa)
            accepted is not null && published.Any(p => p.Version == accepted) ? accepted : null);
    }

    private static string StateCode(PolicyAcceptanceState state) => state switch
    {
        PolicyAcceptanceState.NotPublished => "not_published",
        PolicyAcceptanceState.Accepted => "accepted",
        PolicyAcceptanceState.UpdateOptional => "update_optional",
        PolicyAcceptanceState.UpdateRequired => "update_required",
        _ => "never_accepted",
    };
}
