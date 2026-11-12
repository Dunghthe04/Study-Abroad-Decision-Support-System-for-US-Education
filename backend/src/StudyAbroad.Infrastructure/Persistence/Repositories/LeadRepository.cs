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

        var items = rows
            .Select(r => CenterLeadMapper.ToCenterDto(r.Lead, r.CenterName, r.FullName, r.Email, r.Profile))
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

        return CenterLeadMapper.ToCenterDto(lead.Lead, lead.CenterName, lead.User.FullName, lead.User.Email, lead.Profile);
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
}

