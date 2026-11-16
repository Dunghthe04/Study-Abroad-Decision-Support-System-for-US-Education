namespace StudyAbroad.Application.Leads;

/// <summary>[UC-58] Trung tâm có tin bị gỡ lặp lại, đủ điều kiện để Moderator đề nghị tạm ngừng.</summary>
public record RepeatRemovalCenterDto(
    Guid StudyCenterId,
    string StudyCenterName,
    string VerificationStatus,
    int RemovedMessageCount,
    bool HasPendingProposal);

/// <summary>[UC-58] Đề nghị tạm ngừng trung tâm do Moderator gửi lên Admin.</summary>
public record CenterSuspensionProposalDto(
    Guid Id,
    Guid StudyCenterId,
    string StudyCenterName,
    Guid ProposedByUserId,
    string Reason,
    string Status,
    int RemovedMessageCount,
    DateTime CreatedAt,
    Guid? DecidedByUserId,
    DateTime? DecidedAt,
    string? DecisionNote);

public record CreateSuspensionProposalRequest(Guid StudyCenterId, string? Reason);

/// <summary><paramref name="Approve"/> true: tạm ngừng trung tâm; false: từ chối đề nghị. <paramref name="Note"/> tối đa 1000 ký tự.</summary>
public record DecideSuspensionProposalRequest(bool Approve, string? Note = null);
