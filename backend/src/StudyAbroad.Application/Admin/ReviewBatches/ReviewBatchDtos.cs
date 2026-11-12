namespace StudyAbroad.Application.Admin.ReviewBatches;

/// <summary>
/// Yêu cầu tạo đợt duyệt mẫu. SizePerStratum là số câu muốn bốc cho MỖI nhóm skill. Seed bỏ trống thì hệ thống tự sinh (và lưu lại).
/// From, To là ngày UTC, tính cả hai đầu, bỏ trống là không giới hạn. ReviewerIds là các Admin được giao duyệt.
/// </summary>
public record CreateReviewBatchRequest(
    string? Name, int SizePerStratum, DateOnly? From, DateOnly? To, IReadOnlyList<Guid>? ReviewerIds, string? Seed);

/// <summary>Đợt vừa tạo. Không có nội dung chat. Strata cho biết mỗi nhóm có bao nhiêu câu và chọn được bao nhiêu.</summary>
public record ReviewBatchCreatedDto(
    Guid Id,
    string Name,
    string Seed,
    int SizePerStratum,
    DateTime? SampledFromUtc,
    DateTime? SampledToUtcExclusive,
    int TotalItems,
    IReadOnlyList<StratumCount> Strata,
    IReadOnlyList<Guid> ReviewerIds,
    DateTime CreatedAt);

/// <summary>Một câu trả lời có thể được bốc: mã tin và meta_json của nó (để suy ra nhóm). Không có nội dung chat.</summary>
public record ReviewCandidate(Guid MessageId, string? ReplyMetaJson);
