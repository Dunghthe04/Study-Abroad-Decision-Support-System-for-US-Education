using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Leads;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [UC-42 / UC-44] Repository xử lý lưu trữ và truy vấn Yêu cầu tư vấn (Lead) và sự đồng ý (Consent).
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
                    l.CreatedAt))
            .FirstOrDefaultAsync(ct);

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

    public async Task<PagedResult<LeadDto>> GetMyLeadsAsync(Guid userId, LeadQuery query, CancellationToken ct = default)
    {
        var baseQuery = db.Leads.AsNoTracking()
            .Where(l => l.UserId == userId);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim().ToLowerInvariant();
            baseQuery = baseQuery.Where(l => l.Status.ToLower() == status);
        }

        var total = await baseQuery.CountAsync(ct);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var items = await (from l in baseQuery.OrderByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                           join c in db.StudyCenters.AsNoTracking() on l.StudyCenterId equals c.Id into centers
                           from c in centers.DefaultIfEmpty()
                           select new LeadDto(
                               l.Id,
                               l.UserId,
                               l.StudyCenterId,
                               c != null ? c.Name : null,
                               l.ConsentId,
                               l.StudyLevel,
                               l.ContactPhone,
                               l.Message,
                               l.SharedProfile,
                               l.Status,
                               l.CreatedAt))
                           .ToListAsync(ct);

        return new PagedResult<LeadDto>(items, page, pageSize, total);
    }
}

