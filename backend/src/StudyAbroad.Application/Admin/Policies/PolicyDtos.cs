using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Policies;

// [UC-103] Một phiên bản đầy đủ (kèm toàn văn). Dùng khi mở một bản để xem hoặc soạn.
public sealed record PolicyDocumentDto(
    Guid Id, string Kind, string? Version, string Status, string Title, string Content, string? ChangeSummary,
    bool RequiresReconsent, Guid? BasedOnId, Guid? CreatedByUserId, DateTime CreatedAt,
    DateTime? PublishedAt, Guid? PublishedByUserId, DateTime? RetiredAt);

// [UC-103] Một dòng trong lịch sử: không kèm toàn văn cho gọn, chỉ có độ dài.
public sealed record PolicyDocumentSummaryDto(
    Guid Id, string Kind, string? Version, string Status, string Title, string? ChangeSummary,
    bool RequiresReconsent, int ContentLength, DateTime CreatedAt, DateTime? PublishedAt, DateTime? RetiredAt);

// Kết quả tạo bản nháp. Created = false: loại này đã có bản nháp nên trả lại bản nháp đó, không tạo thêm (A1).
public sealed record PolicyDraftResult(PolicyDocumentDto Document, bool Created);

// Nội dung admin lưu vào bản nháp. ChangeSummary có thể để trống khi đang soạn nhưng bắt buộc lúc công bố.
public sealed record UpdatePolicyDraftRequest(string Title, string Content, string? ChangeSummary, bool RequiresReconsent);

// Người thực hiện và nơi thực hiện, để ghi nhật ký (UC-97).
public sealed record PolicyActor(Guid UserId, string? IpAddress = null, string? UserAgent = null);

// Lỗi nghiệp vụ có mã ổn định để giao diện nhận biết: policy_locked (sửa bản đã công bố), policy_conflict (trùng nháp hoặc số phiên bản).
public sealed class PolicyRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public static class PolicyMapper
{
    public static PolicyDocumentDto ToDto(PolicyDocument d) => new(
        d.Id, d.Kind, d.Version, d.Status, d.Title, d.Content, d.ChangeSummary, d.RequiresReconsent, d.BasedOnId,
        d.CreatedByUserId, d.CreatedAt, d.PublishedAt, d.PublishedByUserId, d.RetiredAt);

    public static PolicyDocumentSummaryDto ToSummary(PolicyDocument d) => new(
        d.Id, d.Kind, d.Version, d.Status, d.Title, d.ChangeSummary, d.RequiresReconsent, d.Content.Length,
        d.CreatedAt, d.PublishedAt, d.RetiredAt);
}
