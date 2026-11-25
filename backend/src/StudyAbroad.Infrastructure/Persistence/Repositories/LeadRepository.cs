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

    public Task<DateTime?> GetLastToCenterAsync(Guid submitterId, Guid centerId, DateTime sinceUtc, CancellationToken ct = default)
    {
        // [UC-116] Lead phụ huynh từ chối / hết hạn không tới trung tâm nên không tính 7 ngày chờ
        var now = DateTime.UtcNow;
        return db.Leads.AsNoTracking()
            .Where(l => l.SubmittedByUserId == submitterId && l.StudyCenterId == centerId && l.CreatedAt >= sinceUtc
                && l.Status != LeadStatuses.Cancelled && l.Status != LeadStatuses.Expired
                && !(l.Status == LeadStatuses.PendingParent && l.ParentTokenExpiresAt <= now))
            .MaxAsync(l => (DateTime?)l.CreatedAt, ct);
    }

    public Task<Lead?> GetByParentTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.ParentTokenHash == tokenHash, ct);

    public async Task<bool> TryCompleteParentConfirmAsync(Guid leadId, LeadParentDecision decision, Consent? consent, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (consent != null)
        {
            db.Consents.Add(consent);
            await db.SaveChangesAsync(ct);
        }

        // UPDATE có điều kiện status = pending_parent: hai phụ huynh bấm cùng lúc thì chỉ một người ghi được
        Guid? consentId = consent?.Id;
        var rows = await db.Leads
            .Where(l => l.Id == leadId && l.Status == LeadStatuses.PendingParent)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Status, decision.Status)
                .SetProperty(l => l.ParentEmail, decision.ParentEmail)
                .SetProperty(l => l.ParentDecidedAt, decision.DecidedAt)
                .SetProperty(l => l.ParentDecisionIp, decision.DecisionIp)
                .SetProperty(l => l.ConsentId, l => consentId ?? l.ConsentId)
                .SetProperty(l => l.UpdatedAt, decision.UpdatedAt), ct);

        if (rows == 0)
        {
            await tx.RollbackAsync(ct);
            if (consent != null)
                db.Entry(consent).State = EntityState.Detached;
            return false;
        }

        await tx.CommitAsync(ct);
        return true;
    }

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
        var now = DateTime.UtcNow;

        var ids = studentIds.ToList();
        var leads = db.Leads.AsNoTracking();
        leads = submitterId is { } sid
            ? leads.Where(l => (l.UserId != null && ids.Contains(l.UserId.Value)) || l.SubmittedByUserId == sid)
            : leads.Where(l => l.UserId != null && ids.Contains(l.UserId.Value));

        var total = await leads.CountAsync(ct);

        var items = await (
                from l in leads
                join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                join u in db.Users.AsNoTracking() on l.UserId equals (Guid?)u.Id
                orderby l.CreatedAt descending
                select new SentLeadDto(
                    l.Id,
                    l.StudyCenterId,
                    c.Name,
                    // [UC-116] Quá hạn mà chưa ai mở link (hết hạn lười) vẫn hiện "Hết hạn"
                    l.Status == LeadStatuses.PendingParent && l.ParentTokenExpiresAt <= now ? LeadStatuses.Expired : l.Status,
                    l.CreatedAt,
                    l.SharedFields,
                    l.StudyLevel,
                    l.ContactPhone,
                    l.Message,
                    u.Id,
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
        // [UC-116] Lead chờ phụ huynh / bị từ chối / hết hạn: trung tâm chưa nhận nên không hiện, không đếm
        var baseLeads = db.Leads.AsNoTracking()
            .Where(l => l.Status != LeadStatuses.PendingParent && l.Status != LeadStatuses.Cancelled && l.Status != LeadStatuses.Expired);
        if (centerId.HasValue && centerId.Value != Guid.Empty)
        {
            baseLeads = baseLeads.Where(l => l.StudyCenterId == centerId.Value);
        }

        var newCount = await baseLeads.CountAsync(l => l.Status == "new", ct);
        // [UC-73] "Đã chấp nhận" gồm cả accepted và contacted (trạng thái cũ)
        var contactedCount = await baseLeads.CountAsync(l => l.Status == "contacted" || l.Status == "accepted", ct);
        var closedCount = await baseLeads.CountAsync(l => l.Status == "closed", ct);
        var rejectedCount = await baseLeads.CountAsync(l => l.Status == "rejected", ct);

        var filteredLeads = baseLeads;
        if (!string.IsNullOrWhiteSpace(status) && !string.Equals(status, "all", StringComparison.OrdinalIgnoreCase))
        {
            var wanted = status.ToLower();
            filteredLeads = wanted is "contacted" or "accepted"
                ? filteredLeads.Where(l => l.Status == "contacted" || l.Status == "accepted")
                : filteredLeads.Where(l => l.Status == wanted);
        }

        var query = from l in filteredLeads
                    join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                    // [UC-75 lớp 1b] Nối trái: lead của người gửi đã bị xóa hẳn (UserId null) vẫn hiện để trung tâm xem tin nhắn còn giữ
                    join u in db.Users.AsNoTracking() on l.UserId equals (Guid?)u.Id into senders
                    from u in senders.DefaultIfEmpty()
                    join p in db.StudentProfiles.AsNoTracking() on l.UserId equals (Guid?)p.UserId into profiles
                    from p in profiles.DefaultIfEmpty()
                    select new { Lead = l, CenterName = c.Name, User = u, Profile = p };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            // [UC-80] Lead đã thu hồi không khớp tìm kiếm, để không lộ tên/SĐT/lời nhắn của người đã xóa tài khoản
            query = query.Where(x => x.Lead.Status != LeadStatuses.Withdrawn);
            // [UC-73] Lead quá 90 ngày cũng không khớp tìm kiếm theo dữ liệu đã chia sẻ
            var visibleSince = DateTime.UtcNow.AddDays(-CenterLeadMapper.DataVisibleDays);
            query = query.Where(x => x.Lead.CreatedAt >= visibleSince);
            query = query.Where(x => (x.Lead.ContactPhone != null && EF.Functions.ILike(x.Lead.ContactPhone, $"%{term}%"))
                                  || (x.Lead.Message != null && EF.Functions.ILike(x.Lead.Message, $"%{term}%"))
                                  || (x.Lead.SharedFields.Contains(LeadSharedFields.Contact) && x.User != null && EF.Functions.ILike(x.User.FullName, $"%{term}%")));
        }

        var filteredTotal = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : (pageSize > 50 ? 50 : pageSize);

        // [UC-42] Lấy dữ liệu thô rồi lọc theo nhóm đã chia sẻ trong CenterLeadMapper
        var rows = await query
            .OrderByDescending(x => x.Lead.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new { x.Lead, x.CenterName, FullName = x.User != null ? x.User.FullName : null, Email = x.User != null ? x.User.Email : null, x.Profile })
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
            closedCount)
        {
            RejectedCount = rejectedCount,
        };
    }

    public async Task<CenterReceivedLeadDto?> GetReceivedLeadDetailAsync(
        Guid leadId,
        Guid? restrictToCenterId,
        CancellationToken ct = default)
    {
        var query = from l in db.Leads.AsNoTracking()
                    where l.Id == leadId
                        // [UC-116] Trung tâm chưa nhận lead này: báo không tìm thấy
                        && l.Status != LeadStatuses.PendingParent && l.Status != LeadStatuses.Cancelled && l.Status != LeadStatuses.Expired
                    join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id
                    // [UC-75 lớp 1b] Nối trái: lead của người gửi đã bị xóa hẳn (UserId null) vẫn hiện để trung tâm xem tin nhắn còn giữ
                    join u in db.Users.AsNoTracking() on l.UserId equals (Guid?)u.Id into senders
                    from u in senders.DefaultIfEmpty()
                    join p in db.StudentProfiles.AsNoTracking() on l.UserId equals (Guid?)p.UserId into profiles
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
        return CenterLeadMapper.ToCenterDto(lead.Lead, lead.CenterName, lead.User?.FullName, lead.User?.Email, lead.Profile, certifications);
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
        Guid? leadOwnerUserId,
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

    /// <summary>
    /// [Chat realtime] Đánh dấu đã xem các tin còn hiện của phía đối diện. Phía trung tâm gồm các vai trò trung tâm /
    /// kiểm duyệt / quản trị (cùng định nghĩa với <see cref="LeadMessageMapper.IsStaffOrCenterRole"/>).
    /// </summary>
    public async Task<IReadOnlyList<Guid>> MarkMessagesReadAsync(
        Guid leadId,
        bool readerIsCenterSide,
        DateTime readAt,
        CancellationToken ct = default)
    {
        string[] staffRoles = [UserRoles.Center, UserRoles.Moderator, UserRoles.Admin];

        var unread = from m in db.LeadMessages
                     join u in db.Users on m.SenderUserId equals (Guid?)u.Id
                     where m.LeadId == leadId && m.ReadAt == null && m.Status == "visible"
                     select new { Message = m, u.Role };

        // Trung tâm đọc thì đánh dấu tin của học sinh / phụ huynh, và ngược lại
        var rows = await (readerIsCenterSide
                ? unread.Where(x => !staffRoles.Contains(x.Role))
                : unread.Where(x => staffRoles.Contains(x.Role)))
            .ToListAsync(ct);

        foreach (var row in rows) row.Message.ReadAt = readAt;
        if (rows.Count > 0) await db.SaveChangesAsync(ct);

        return rows.Select(x => x.Message.Id).ToList();
    }

    /// <summary>[UC-58] Hàng đợi báo cáo của Admin: tin bị báo cáo đang chờ xử lý, tin đã gỡ, hoặc cả hai.</summary>
    public async Task<PagedResult<AdminLeadMessageReportDto>> GetReportedMessagesAsync(
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
                    // [UC-75 lớp 1b] Nối trái: tin của người đã bị xóa hẳn còn giữ khi báo cáo chưa xử lý xong
                    join u in db.Users.AsNoTracking() on m.SenderUserId equals (Guid?)u.Id into senders
                    from u in senders.DefaultIfEmpty()
                    where m.ReportedAt != null
                    select new { Message = m, l.StudyCenterId, CenterName = c.Name, Sender = u };

        query = filter switch
        {
            "resolved" => query.Where(x => x.Message.Status == "removed" || x.Message.ReportResolution != null),
            "all" => query,
            _ => query.Where(x => x.Message.Status != "removed" && x.Message.ReportResolution == null),
        };

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Message.ReportedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new AdminLeadMessageReportDto(
            x.Message.Id,
            x.Message.LeadId,
            x.StudyCenterId,
            x.CenterName,
            x.Message.SenderUserId,
            x.Sender?.FullName ?? LeadMessageMapper.DeletedUserName,
            x.Sender?.Email ?? string.Empty,
            x.Sender?.Role ?? UserRoles.Student,
            x.Sender?.Status ?? UserStatuses.Deleted,
            LeadMessageMapper.IsStaffOrCenterRole(x.Sender?.Role),
            x.Message.Content,
            x.Message.AttachmentUrl,
            x.Message.Status,
            x.Message.ReportedByUserId,
            x.Message.ReportedAt,
            x.Message.ReportReason,
            x.Message.CreatedAt,
            x.Message.ReportResolution,
            x.Message.ReportResolvedAt)).ToList();

        return new PagedResult<AdminLeadMessageReportDto>(items, safePage, safePageSize, total);
    }

    public async Task<IReadOnlyList<AdminReportContextMessageDto>> GetReportContextMessagesAsync(
        Guid leadId,
        Guid messageId,
        int contextSize,
        CancellationToken ct = default)
    {
        var thread = db.LeadMessages.AsNoTracking().Where(m => m.LeadId == leadId);
        var target = await thread.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (target is null) return [];

        var size = Math.Max(contextSize, 0);
        var before = size == 0 ? [] : await thread
            .Where(m => m.Id != target.Id && m.CreatedAt <= target.CreatedAt)
            .OrderByDescending(m => m.CreatedAt)
            .Take(size)
            .ToListAsync(ct);
        var beforeIds = before.Select(m => m.Id).ToList();
        var after = size == 0 ? [] : await thread
            .Where(m => m.Id != target.Id && !beforeIds.Contains(m.Id) && m.CreatedAt >= target.CreatedAt)
            .OrderBy(m => m.CreatedAt)
            .Take(size)
            .ToListAsync(ct);

        var window = before.OrderBy(m => m.CreatedAt).Append(target).Concat(after).ToList();
        var senderIds = window.Where(m => m.SenderUserId != null).Select(m => m.SenderUserId!.Value).Distinct().ToList();
        var senders = await db.Users.AsNoTracking()
            .Where(u => senderIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Role })
            .ToDictionaryAsync(u => u.Id, ct);

        return window.Select(m =>
        {
            var sender = m.SenderUserId is { } senderId ? senders.GetValueOrDefault(senderId) : null;
            return new AdminReportContextMessageDto(
                m.Id,
                m.SenderUserId,
                m.SenderUserId is null ? LeadMessageMapper.DeletedUserName : sender?.FullName ?? "Người dùng",
                LeadMessageMapper.IsStaffOrCenterRole(sender?.Role),
                m.Content,
                m.AttachmentUrl,
                m.Status,
                m.CreatedAt,
                m.Id == target.Id);
        }).ToList();
    }

    public async Task<int?> GetReportContextSizeSettingAsync(CancellationToken ct = default)
    {
        var json = await db.AppSettings.AsNoTracking()
            .Where(s => s.Key == LeadService.ReportContextSizeSettingKey)
            .Select(s => s.ValueJson)
            .FirstOrDefaultAsync(ct);
        return LeadService.ParseReportContextSize(json);
    }

    public async Task<int> RemoveAllMessagesOfUserAsync(Guid senderUserId, Guid removedByUserId, CancellationToken ct = default)
    {
        var messages = await db.LeadMessages
            .Where(m => m.SenderUserId == senderUserId && m.Status != "removed")
            .ToListAsync(ct);
        foreach (var m in messages)
        {
            m.Status = "removed";
            m.RemovedByUserId = removedByUserId;
        }
        await db.SaveChangesAsync(ct);
        return messages.Count;
    }

    public async Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRoles.Admin && u.Status == UserStatuses.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task AddNotificationsAsync(IEnumerable<Notification> notifications, CancellationToken ct = default)
    {
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveLeadMessageAsync(LeadMessage message, CancellationToken ct = default)
    {
        db.LeadMessages.Update(message);
        await db.SaveChangesAsync(ct);
    }
}

