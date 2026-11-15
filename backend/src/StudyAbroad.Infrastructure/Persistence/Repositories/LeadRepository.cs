using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [UC-42] Repository xử lý lưu trữ Yêu cầu tư vấn (Lead) và sự đồng ý (Consent).
/// </summary>
public class LeadRepository(AppDbContext db) : ILeadRepository
{
    public Task<Lead?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<LeadDto?> GetDtoByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Leads.AsNoTracking()
            .Where(l => l.Id == id)
            .Join(db.StudyCenters.AsNoTracking(),
                l => l.StudyCenterId,
                c => c.Id,
                (l, c) => new LeadDto(
                    l.Id,
                    l.UserId,
                    l.StudyCenterId,
                    c.Name,
                    l.ConsentId,
                    l.StudyLevel,
                    l.ContactPhone,
                    l.Message,
                    l.SharedProfile,
                    l.Status,
                    l.CreatedAt)
                {
                    SubmittedByUserId = l.SubmittedByUserId,
                    SharedFields = l.SharedFields,
                })
            .FirstOrDefaultAsync(ct);

    public Task<int> CountBySubmitterSinceAsync(Guid submitterId, DateTime sinceUtc, CancellationToken ct = default) =>
        db.Leads.AsNoTracking()
            .CountAsync(l => l.SubmittedByUserId == submitterId && l.CreatedAt >= sinceUtc, ct);

    public Task<DateTime?> GetLastToCenterAsync(Guid submitterId, Guid centerId, DateTime sinceUtc, CancellationToken ct = default) =>
        db.Leads.AsNoTracking()
            .Where(l => l.SubmittedByUserId == submitterId && l.StudyCenterId == centerId && l.CreatedAt >= sinceUtc)
            .MaxAsync(l => (DateTime?)l.CreatedAt, ct);

    public async Task<Lead> CreateLeadAsync(Lead lead, Consent? consent = null, CancellationToken ct = default)
    {
        if (consent != null)
        {
            db.Consents.Add(consent);
            lead.ConsentId = consent.Id;
        }

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);
        return lead;
    }

    /// <summary>
    /// [UC-44] Yêu cầu liên hệ đã gửi của học sinh / phụ huynh, mới nhất trước.
    /// Đếm tin nhắn phía trung tâm = tin đang hiện mà người gửi không phải học sinh của lead
    /// và không phải người đã bấm gửi lead.
    /// </summary>
    public async Task<PagedResult<SentLeadDto>> GetSentLeadsAsync(
        IReadOnlyCollection<Guid> studentIds,
        Guid? submitterId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : (pageSize > 50 ? 50 : pageSize);

        var ids = studentIds.ToList();
        var leads = db.Leads.AsNoTracking();
        leads = submitterId is { } sid
            ? leads.Where(l => ids.Contains(l.UserId) || l.SubmittedByUserId == sid)
            : leads.Where(l => ids.Contains(l.UserId));

        var total = await leads.CountAsync(ct);

        var items = await (
                from l in leads
                join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                join u in db.Users.AsNoTracking() on l.UserId equals u.Id
                orderby l.CreatedAt descending
                select new SentLeadDto(
                    l.Id,
                    l.StudyCenterId,
                    c.Name,
                    l.Status,
                    l.CreatedAt,
                    l.SharedFields,
                    l.StudyLevel,
                    l.ContactPhone,
                    l.Message,
                    l.UserId,
                    u.FullName,
                    l.SubmittedByUserId != null && l.SubmittedByUserId != l.UserId,
                    db.LeadMessages.Count(m => m.LeadId == l.Id
                                            && m.Status == "visible"
                                            && m.SenderUserId != l.UserId
                                            && m.SenderUserId != l.SubmittedByUserId)))
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        return new PagedResult<SentLeadDto>(items, safePage, safePageSize, total);
    }

    public async Task<CenterLeadsSummaryDto> GetReceivedLeadsAsync(
        Guid? centerId,
        string? status,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var baseLeads = db.Leads.AsNoTracking();
        if (centerId.HasValue && centerId.Value != Guid.Empty)
        {
            baseLeads = baseLeads.Where(l => l.StudyCenterId == centerId.Value);
        }

        var newCount = await baseLeads.CountAsync(l => l.Status == "new", ct);
        var contactedCount = await baseLeads.CountAsync(l => l.Status == "contacted", ct);
        var closedCount = await baseLeads.CountAsync(l => l.Status == "closed", ct);

        var filteredLeads = baseLeads;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            filteredLeads = filteredLeads.Where(l => l.Status == status.ToLower());
        }

        var query = from l in filteredLeads
                    join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                    join u in db.Users.AsNoTracking() on l.UserId equals u.Id
                    join p in db.StudentProfiles.AsNoTracking() on l.UserId equals p.UserId into profiles
                    from p in profiles.DefaultIfEmpty()
                    select new { Lead = l, CenterName = c.Name, User = u, Profile = p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => (x.Lead.ContactPhone != null && EF.Functions.ILike(x.Lead.ContactPhone, $"%{term}%"))
                                  || (x.Lead.Message != null && EF.Functions.ILike(x.Lead.Message, $"%{term}%"))
                                  || (x.Lead.SharedFields.Contains(LeadSharedFields.Contact) && EF.Functions.ILike(x.User.FullName, $"%{term}%")));
        }

        var filteredTotal = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : (pageSize > 50 ? 50 : pageSize);

        // [UC-42] Lấy dữ liệu thô rồi lọc theo nhóm đã chia sẻ trong CenterLeadMapper
        var rows = await query
            .OrderByDescending(x => x.Lead.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new { x.Lead, x.CenterName, x.User.FullName, x.User.Email, x.Profile })
            .ToListAsync(ct);

        var certifications = await CertificationsByProfileAsync(rows.Where(r => r.Profile != null).Select(r => r.Profile!.Id), ct);
        var items = rows
            .Select(r => CenterLeadMapper.ToCenterDto(r.Lead, r.CenterName, r.FullName, r.Email, r.Profile,
                r.Profile is null ? null : certifications.GetValueOrDefault(r.Profile.Id, [])))
            .ToList();

        return new CenterLeadsSummaryDto(
            items,
            filteredTotal,
            safePage,
            safePageSize,
            newCount,
            contactedCount,
            closedCount);
    }

    public async Task<CenterReceivedLeadDto?> GetReceivedLeadDetailAsync(
        Guid leadId,
        Guid? restrictToCenterId,
        CancellationToken ct = default)
    {
        var query = from l in db.Leads.AsNoTracking()
                    where l.Id == leadId
                    join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                    join u in db.Users.AsNoTracking() on l.UserId equals u.Id
                    join p in db.StudentProfiles.AsNoTracking() on l.UserId equals p.UserId into profiles
                    from p in profiles.DefaultIfEmpty()
                    select new { Lead = l, CenterName = c.Name, User = u, Profile = p };

        if (restrictToCenterId.HasValue && restrictToCenterId.Value != Guid.Empty)
        {
            query = query.Where(x => x.Lead.StudyCenterId == restrictToCenterId.Value);
        }

        var lead = await query.FirstOrDefaultAsync(ct);
        if (lead is null) return null;

        var certifications = lead.Profile is null
            ? null
            : (await CertificationsByProfileAsync([lead.Profile.Id], ct)).GetValueOrDefault(lead.Profile.Id, []);
        return CenterLeadMapper.ToCenterDto(lead.Lead, lead.CenterName, lead.User.FullName, lead.User.Email, lead.Profile, certifications);
    }

    // Chứng chỉ và dự định thi của các hồ sơ, để trung tâm tách điểm đã thi với điểm mục tiêu
    private async Task<Dictionary<Guid, IReadOnlyList<ProfileCertification>>> CertificationsByProfileAsync(
        IEnumerable<Guid> profileIds, CancellationToken ct)
    {
        var ids = profileIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var rows = await db.ProfileCertifications.AsNoTracking()
            .Where(c => ids.Contains(c.StudentProfileId))
            .ToListAsync(ct);
        return rows.GroupBy(c => c.StudentProfileId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ProfileCertification>)g.OrderBy(c => c.SortOrder).ToList());
    }

    public Task<Lead?> GetLeadForUpdateAsync(Guid leadId, CancellationToken ct = default) =>
        db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);

    public async Task UpdateAsync(Lead lead, CancellationToken ct = default)
    {
        lead.UpdatedAt = DateTime.UtcNow;
        db.Leads.Update(lead);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// [USAS-2053 / UC-74] Truy vấn toàn bộ tin nhắn trong một yêu cầu liên hệ từ bảng app.lead_messages,
    /// sắp xếp tăng dần theo thời gian tạo. Tin nhắn bị gỡ được che nội dung; tên người gửi là học sinh/phụ huynh
    /// được ẩn danh khi <paramref name="sharedProfile"/> = false (service truyền: lead có chia sẻ nhóm contact không).
    /// </summary>
    public async Task<IReadOnlyList<LeadMessageDto>> GetLeadMessagesAsync(
        Guid leadId,
        Guid leadOwnerUserId,
        bool sharedProfile,
        string centerName,
        CancellationToken ct = default)
    {
        var rows = await LeadMessageMapper
            .QueryThreadRows(db.LeadMessages.AsNoTracking(), db.Users.AsNoTracking(), leadId)
            .ToListAsync(ct);

        return rows
            .Select(row => LeadMessageMapper.ToDto(row, sharedProfile, centerName))
            .ToList();
    }

    /// <summary>
    /// [USAS-2053 / UC-74] Lưu tin nhắn mới vào bảng app.lead_messages (và cập nhật trạng thái lead nếu cần).
    /// </summary>
    public async Task<LeadMessage> AddLeadMessageAsync(
        LeadMessage message,
        Lead? leadToUpdate = null,
        CancellationToken ct = default)
    {
        db.LeadMessages.Add(message);

        if (leadToUpdate is not null)
        {
            leadToUpdate.UpdatedAt = DateTime.UtcNow;
            if (db.Entry(leadToUpdate).State == EntityState.Detached)
            {
                db.Leads.Update(leadToUpdate);
            }
        }

        await db.SaveChangesAsync(ct);
        return message;
    }

    /// <summary>[USAS-2080 / UC-58] Lấy tin nhắn theo đúng lead (tin của lead khác coi như không có) để gỡ.</summary>
    public Task<LeadMessage?> GetLeadMessageForUpdateAsync(Guid leadId, Guid messageId, CancellationToken ct = default) =>
        db.LeadMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.LeadId == leadId, ct);

    /// <summary>[USAS-2080 / UC-58] Hàng đợi kiểm duyệt: tin gắn cờ đang hiện, tin đã gỡ, hoặc cả hai.</summary>
    public async Task<PagedResult<ModerationLeadMessageDto>> GetModerationMessagesAsync(
        string filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 20 : (pageSize > 50 ? 50 : pageSize);

        var query = from m in db.LeadMessages.AsNoTracking()
                    join l in db.Leads.AsNoTracking() on m.LeadId equals l.Id
                    join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                    join u in db.Users.AsNoTracking() on m.SenderUserId equals u.Id into senders
                    from u in senders.DefaultIfEmpty()
                    select new { Message = m, l.StudyCenterId, CenterName = c.Name, SenderRole = u != null ? u.Role : null };

        query = filter switch
        {
            "removed" => query.Where(x => x.Message.Status == "removed"),
            "flagged" => query.Where(x => x.Message.IsFlagged && x.Message.Status != "removed"),
            "reported" => query.Where(x => x.Message.ReportedAt != null && x.Message.Status != "removed"),
            "all" => query.Where(x => x.Message.Status == "removed" || x.Message.IsFlagged || x.Message.ReportedAt != null),
            // pending: tin đang hiện bị gắn cờ tự động hoặc bị báo cáo, chờ Moderator xem
            _ => query.Where(x => (x.Message.IsFlagged || x.Message.ReportedAt != null) && x.Message.Status != "removed"),
        };

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Message.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            var role = x.SenderRole ?? UserRoles.Student;
            return new ModerationLeadMessageDto(
                x.Message.Id,
                x.Message.LeadId,
                x.StudyCenterId,
                x.CenterName,
                x.Message.SenderUserId,
                role,
                LeadMessageMapper.IsStaffOrCenterRole(role),
                x.Message.Content,
                x.Message.IsFlagged,
                x.Message.FlagReason,
                x.Message.Status,
                x.Message.RemovedByUserId,
                x.Message.CreatedAt)
            {
                ReportedAt = x.Message.ReportedAt,
                ReportReason = x.Message.ReportReason,
            };
        }).ToList();

        return new PagedResult<ModerationLeadMessageDto>(items, safePage, safePageSize, total);
    }

    /// <summary>[UC-58] Tin do phía trung tâm gửi (người gửi có vai trò trung tâm) đã bị gỡ, tính theo trung tâm nhận yêu cầu.</summary>
    public Task<int> CountRemovedCenterMessagesAsync(Guid centerId, CancellationToken ct = default) =>
        (from m in db.LeadMessages.AsNoTracking()
         where m.Status == "removed"
         join l in db.Leads.AsNoTracking() on m.LeadId equals l.Id
         where l.StudyCenterId == centerId
         join u in db.Users.AsNoTracking() on m.SenderUserId equals u.Id
         where u.Role == UserRoles.Center
         select m.Id).CountAsync(ct);

    public async Task<IReadOnlyList<RepeatRemovalCenterDto>> GetRepeatRemovalCentersAsync(int minRemoved, CancellationToken ct = default)
    {
        var counts = await (from m in db.LeadMessages.AsNoTracking()
                            where m.Status == "removed"
                            join l in db.Leads.AsNoTracking() on m.LeadId equals l.Id
                            join u in db.Users.AsNoTracking() on m.SenderUserId equals u.Id
                            where u.Role == UserRoles.Center
                            group m by l.StudyCenterId into g
                            select new { CenterId = g.Key, Count = g.Count() })
            .Where(x => x.Count >= minRemoved)
            .ToListAsync(ct);
        if (counts.Count == 0) return [];

        var ids = counts.Select(x => x.CenterId).ToList();
        var centers = await db.StudyCenters.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, c.VerificationStatus })
            .ToListAsync(ct);
        var pending = (await db.CenterSuspensionProposals.AsNoTracking()
                .Where(p => p.Status == CenterSuspensionProposalStatuses.Pending && ids.Contains(p.StudyCenterId))
                .Select(p => p.StudyCenterId)
                .ToListAsync(ct))
            .ToHashSet();

        return counts
            .Join(centers, x => x.CenterId, c => c.Id,
                (x, c) => new RepeatRemovalCenterDto(c.Id, c.Name, c.VerificationStatus, x.Count, pending.Contains(c.Id)))
            .OrderByDescending(x => x.RemovedMessageCount)
            .ToList();
    }

    public async Task SaveLeadMessageAsync(LeadMessage message, CancellationToken ct = default)
    {
        db.LeadMessages.Update(message);
        await db.SaveChangesAsync(ct);
    }
}

