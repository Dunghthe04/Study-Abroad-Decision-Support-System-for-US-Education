using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Leads;
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
                                  || (x.Lead.SharedProfile && EF.Functions.ILike(x.User.FullName, $"%{term}%")));
        }

        var filteredTotal = await query.CountAsync(ct);
        var safePage = page < 1 ? 1 : page;
        var safePageSize = pageSize < 1 ? 10 : (pageSize > 50 ? 50 : pageSize);

        var items = await query
            .OrderByDescending(x => x.Lead.CreatedAt)
            .Skip((safePage - 1) * safePageSize)
            .Take(safePageSize)
            .Select(x => new CenterReceivedLeadDto(
                x.Lead.Id,
                x.Lead.StudyCenterId,
                x.CenterName,
                x.Lead.StudyLevel,
                x.Lead.ContactPhone,
                x.Lead.Message,
                x.Lead.SharedProfile,
                x.Lead.Status,
                x.Lead.CreatedAt,
                x.Lead.SharedProfile ? x.User.FullName : null,
                x.Lead.SharedProfile ? x.User.Email : null,
                x.Lead.SharedProfile && x.Profile != null
                    ? new SharedProfileSummaryDto(
                        x.Profile.OverallGpa,
                        x.Profile.TargetLevel,
                        x.Profile.IntendedMajor,
                        x.Profile.EducationSystem,
                        x.Profile.CurrentGrade,
                        x.Profile.Ielts ?? x.Profile.Toefl ?? x.Profile.Duolingo,
                        x.Profile.EnglishTestType,
                        x.Profile.Sat,
                        x.Profile.Act,
                        x.Profile.ExtracurricularTier,
                        x.Profile.BudgetTier)
                    : null
            ))
            .ToListAsync(ct);

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

        return new CenterReceivedLeadDto(
            lead.Lead.Id,
            lead.Lead.StudyCenterId,
            lead.CenterName,
            lead.Lead.StudyLevel,
            lead.Lead.ContactPhone,
            lead.Lead.Message,
            lead.Lead.SharedProfile,
            lead.Lead.Status,
            lead.Lead.CreatedAt,
            lead.Lead.SharedProfile ? lead.User.FullName : null,
            lead.Lead.SharedProfile ? lead.User.Email : null,
            lead.Lead.SharedProfile && lead.Profile != null
                ? new SharedProfileSummaryDto(
                    lead.Profile.OverallGpa,
                    lead.Profile.TargetLevel,
                    lead.Profile.IntendedMajor,
                    lead.Profile.EducationSystem,
                    lead.Profile.CurrentGrade,
                    lead.Profile.Ielts ?? lead.Profile.Toefl ?? lead.Profile.Duolingo,
                    lead.Profile.EnglishTestType,
                    lead.Profile.Sat,
                    lead.Profile.Act,
                    lead.Profile.ExtracurricularTier,
                    lead.Profile.BudgetTier)
                : null
        );
    }

    public Task<Lead?> GetLeadForUpdateAsync(Guid leadId, CancellationToken ct = default) =>
        db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);

    public async Task UpdateAsync(Lead lead, CancellationToken ct = default)
    {
        lead.UpdatedAt = DateTime.UtcNow;
        db.Leads.Update(lead);
        await db.SaveChangesAsync(ct);
    }
}

