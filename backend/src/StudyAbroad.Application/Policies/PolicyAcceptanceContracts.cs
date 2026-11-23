using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Policies;

/// <summary>Phần đầu của một phiên bản đã công bố (không kèm toàn văn, để đọc nhẹ): đủ để xét luật và hiện tiêu đề, tóm tắt thay đổi.</summary>
public sealed record PublishedPolicyHeader(string Version, bool RequiresReconsent, string Title, string? ChangeSummary);

/// <summary>[UC-104] Đọc các phiên bản đã công bố (đang hiệu lực và đã hết hiệu lực) của một loại tài liệu. Bản nháp không có ở đây.</summary>
public interface IPublishedPolicyReader
{
    Task<IReadOnlyList<PublishedPolicyHeader>> ListPublishedAsync(string kind, CancellationToken ct = default);
}

/// <summary>[UC-104] Đọc và ghi bằng chứng đồng ý của tài khoản trong bảng consents. Chỉ thêm dòng mới, không sửa dòng cũ.</summary>
public interface IConsentRepository
{
    /// <summary>Lần đồng ý hoặc từ chối GẦN NHẤT của người dùng với mục đích này; null nếu chưa có dòng nào.</summary>
    Task<Consent?> GetLatestAsync(Guid userId, string purpose, CancellationToken ct = default);

    Task AddAsync(Consent consent, CancellationToken ct = default);

    /// <summary>Mọi lần đồng ý hoặc từ chối của người dùng với các mục đích này, MỚI NHẤT TRƯỚC.</summary>
    Task<IReadOnlyList<Consent>> ListAsync(Guid userId, IReadOnlyCollection<string> purposes, CancellationToken ct = default);
}

// ---------------------------------------------------------------- dữ liệu trả về / nhận vào

/// <summary>Tình trạng đồng ý của người dùng với MỘT loại tài liệu. State: not_published | accepted | update_optional | update_required | never_accepted.</summary>
public sealed record PolicyAcceptanceItemDto(
    string Kind,
    string State,
    /// <summary>Tình trạng này có chặn người dùng dùng tiếp không (update_required hoặc never_accepted).</summary>
    bool Blocks,
    /// <summary>Bản đang hiệu lực; null khi loại này chưa công bố.</summary>
    string? CurrentVersion,
    string? Title,
    /// <summary>Bản mới có gì thay đổi, để hiện cho người dùng trước khi đồng ý lại.</summary>
    string? ChangeSummary,
    /// <summary>Phiên bản người dùng đã đồng ý gần nhất; null nếu chưa từng hoặc lần gần nhất là từ chối.</summary>
    string? AcceptedVersion);

public sealed record PolicyAcceptanceStatusDto(
    /// <summary>Có ít nhất một loại đang chặn: người dùng phải đồng ý mới dùng tiếp.</summary>
    bool MustAccept,
    /// <summary>Có bản mới (kể cả không bắt buộc) để báo cho người dùng biết.</summary>
    bool HasUpdates,
    IReadOnlyList<PolicyAcceptanceItemDto> Items);

/// <summary>Một mục người dùng đồng ý: loại tài liệu và đúng phiên bản họ đã đọc trên màn hình.</summary>
public sealed record AcceptPolicyItem(string Kind, string Version);

/// <summary>Một lần đồng ý hoặc từ chối trong lịch sử của người dùng. Không kèm IP hay thông tin kỹ thuật.</summary>
public sealed record PolicyConsentRecordDto(
    string Kind,
    string Version,
    /// <summary>true: đồng ý; false: từ chối.</summary>
    bool Granted,
    DateTime AcceptedAt,
    /// <summary>Đây là lần gần nhất của loại này, tức tình trạng hiện tại của người dùng.</summary>
    bool IsLatest);

public sealed record PolicyAcceptanceHistoryDto(IReadOnlyList<PolicyConsentRecordDto> Items);

public sealed record AcceptPoliciesRequest(IReadOnlyList<AcceptPolicyItem>? Items);
