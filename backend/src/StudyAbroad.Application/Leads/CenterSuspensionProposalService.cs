using System.Text.Json;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Leads;

/// <summary>
/// [USAS-2080 / UC-58] Gỡ lặp lại nhiều lần tin của một trung tâm thì Moderator đề nghị tạm ngừng lên Admin (đặc tả 5.4);
/// Admin quyết định tạm ngừng hoặc từ chối (đặc tả 5.2). Tạm ngừng dùng cùng trạng thái "suspended" của luồng xác minh.
/// </summary>
public class CenterSuspensionProposalService(
    ILeadRepository leads,
    IStudyCenterRepository studyCenters,
    ICenterSuspensionProposalRepository proposals,
    IAuditLogRepository auditLogs) : ICenterSuspensionProposalService
{
    /// <summary>Số tin bị gỡ tối thiểu để được đề nghị tạm ngừng. Đặc tả chỉ ghi "nhiều lần" nên đây là giá trị tạm, cần BA xác nhận.</summary>
    public const int MinRemovedMessages = 3;
    public const int MaxReasonLength = 1000;

    public Task<IReadOnlyList<RepeatRemovalCenterDto>> GetRepeatRemovalCentersAsync(CancellationToken ct = default) =>
        leads.GetRepeatRemovalCentersAsync(MinRemovedMessages, ct);

    public async Task<CenterSuspensionProposalDto> ProposeAsync(
        Guid moderatorUserId,
        Guid centerId,
        string? reason,
        CancellationToken ct = default)
    {
        if (centerId == Guid.Empty)
            throw new ArgumentException("Mã trung tâm không hợp lệ.");

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason))
            throw new ArgumentException("Vui lòng nhập lý do đề nghị tạm ngừng.");
        if (trimmedReason.Length > MaxReasonLength)
            throw new ArgumentException($"Lý do tối đa {MaxReasonLength} ký tự.");

        var center = await studyCenters.GetByIdAsync(centerId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm.");
        if (center.VerificationStatus == CenterVerificationStatuses.Suspended)
            throw new ArgumentException("Trung tâm này đã bị tạm ngừng.");

        var removed = await leads.CountRemovedCenterMessagesAsync(centerId, ct);
        if (removed < MinRemovedMessages)
            throw new ArgumentException(
                $"Trung tâm mới có {removed} tin bị gỡ, cần từ {MinRemovedMessages} tin trở lên mới đề nghị tạm ngừng.");

        if (await proposals.HasPendingAsync(centerId, ct))
            throw new ArgumentException("Trung tâm này đã có đề nghị tạm ngừng đang chờ Admin quyết định.");

        var proposal = new CenterSuspensionProposal
        {
            StudyCenterId = centerId,
            ProposedByUserId = moderatorUserId,
            Reason = trimmedReason,
            RemovedMessageCount = removed,
            Status = CenterSuspensionProposalStatuses.Pending,
        };
        await proposals.AddAsync(proposal, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = moderatorUserId,
            Action = "center.suspension_proposed",
            EntityType = "StudyCenter",
            EntityId = centerId.ToString(),
            DetailJson = JsonSerializer.Serialize(new { proposalId = proposal.Id, removedMessageCount = removed, reason = trimmedReason }),
        }, ct);

        return ToDto(proposal, center.Name);
    }

    public Task<PagedResult<CenterSuspensionProposalDto>> ListAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var normalized = status?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(normalized) && normalized is not ("all" or "pending" or "approved" or "rejected"))
            throw new ArgumentException("Trạng thái không hợp lệ. Chỉ nhận 'pending', 'approved', 'rejected' hoặc 'all'.");

        return proposals.ListAsync(string.IsNullOrEmpty(normalized) ? "pending" : normalized, page, pageSize, ct);
    }

    public async Task<CenterSuspensionProposalDto> DecideAsync(
        Guid adminUserId,
        Guid proposalId,
        bool approve,
        string? note,
        CancellationToken ct = default)
    {
        if (proposalId == Guid.Empty)
            throw new ArgumentException("Mã đề nghị không hợp lệ.");

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote is { Length: > MaxReasonLength })
            throw new ArgumentException($"Ghi chú tối đa {MaxReasonLength} ký tự.");

        var proposal = await proposals.GetByIdForUpdateAsync(proposalId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy đề nghị tạm ngừng.");
        if (proposal.Status != CenterSuspensionProposalStatuses.Pending)
            throw new ArgumentException("Đề nghị này đã được quyết định.");

        var center = await studyCenters.GetByIdForUpdateAsync(proposal.StudyCenterId, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy trung tâm.");

        if (approve)
        {
            center.VerificationStatus = CenterVerificationStatuses.Suspended;
            center.VerificationNote = trimmedNote ?? proposal.Reason;
            await studyCenters.UpdateAsync(center, ct);
        }

        proposal.Status = approve ? CenterSuspensionProposalStatuses.Approved : CenterSuspensionProposalStatuses.Rejected;
        proposal.DecidedByUserId = adminUserId;
        proposal.DecidedAt = DateTime.UtcNow;
        proposal.DecisionNote = trimmedNote;
        await proposals.SaveAsync(proposal, ct);

        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = adminUserId,
            Action = approve ? "center.suspended" : "center.suspension_rejected",
            EntityType = "StudyCenter",
            EntityId = center.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { proposalId = proposal.Id, note = trimmedNote }),
        }, ct);

        return ToDto(proposal, center.Name);
    }

    private static CenterSuspensionProposalDto ToDto(CenterSuspensionProposal p, string centerName) => new(
        p.Id, p.StudyCenterId, centerName, p.ProposedByUserId, p.Reason, p.Status, p.RemovedMessageCount,
        p.CreatedAt, p.DecidedByUserId, p.DecidedAt, p.DecisionNote);
}
