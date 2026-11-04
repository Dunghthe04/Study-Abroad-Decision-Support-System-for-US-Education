using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.Universities;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AdminUniversityRepository(AppDbContext db) : IAdminUniversityRepository
{
    public async Task<PagedResult<AdminUniversityListItemDto>> SearchAsync(AdminUniversityQuery query, CancellationToken ct = default)
    {
        var q = db.Universities.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(u => EF.Functions.ILike(u.Name, pattern) || EF.Functions.ILike(u.Code, pattern));
        }
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            var state = query.State.Trim().ToUpperInvariant();
            q = q.Where(u => u.State == state);
        }
        if (!string.IsNullOrWhiteSpace(query.Control))
        {
            var control = query.Control.Trim().ToLowerInvariant();
            q = q.Where(u => u.Control == control);
        }
        if (query.IsActive is not null)
            q = q.Where(u => u.IsActive == query.IsActive);

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new AdminUniversityListItemDto(
                u.Id,
                u.Code,
                u.Name,
                u.City,
                u.State,
                u.Control,
                u.AcceptanceRate,
                u.IsActive,
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id).Select(o => o.StudyLevel).ToList(),
                db.UniversityOfferings.Where(o => o.UniversityId == u.Id).Max(o => o.RetrievedAt),
                db.Media.Where((m) => m.EntityType == "University" && m.EntityId == u.Id && m.Type == "logo").Select(m => m.Url).FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<AdminUniversityListItemDto>(items, query.Page, query.PageSize, total);
    }

    public Task<University?> GetUniversityAsync(Guid universityId, CancellationToken ct = default) =>
        db.Universities.FirstOrDefaultAsync(u => u.Id == universityId, ct);

    public async Task<IReadOnlyList<UniversityOffering>> ListOfferingsAsync(Guid universityId, CancellationToken ct = default) =>
        await db.UniversityOfferings.AsNoTracking()
            .Where(o => o.UniversityId == universityId)
            .OrderBy(o => o.StudyLevel)
            .ToListAsync(ct);

    public Task<UniversityOffering?> GetOfferingAsync(Guid universityId, Guid offeringId, CancellationToken ct = default) =>
        db.UniversityOfferings.FirstOrDefaultAsync(o => o.Id == offeringId && o.UniversityId == universityId, ct);

    public async Task<IReadOnlyList<AdminOfferingMajorRow>> ListMajorRowsAsync(IReadOnlyList<Guid> offeringIds, CancellationToken ct = default) =>
        await db.UniversityOfferingMajors.AsNoTracking()
            .Where(link => offeringIds.Contains(link.UniversityOfferingId))
            .Join(
                db.Majors,
                link => link.MajorId,
                major => major.Id,
                (link, major) => new AdminOfferingMajorRow(
                    link.UniversityOfferingId, major.Id, major.Name, major.CipCode, link.IsStem, link.IsVerified))
            .ToListAsync(ct);

    public Task<List<UniversityOfferingMajor>> GetMajorLinksAsync(Guid offeringId, CancellationToken ct = default) =>
        db.UniversityOfferingMajors.Where(link => link.UniversityOfferingId == offeringId).ToListAsync(ct);

    public async Task<IReadOnlyList<Major>> ListMajorsAsync(CancellationToken ct = default) =>
        await db.Majors.AsNoTracking().OrderBy(m => m.Name).ToListAsync(ct);

    public async Task<string?> GetUserDisplayNameAsync(Guid? userId, CancellationToken ct = default)
    {
        if (userId is null) return null;
        return await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct);
    }

    public void AddMajorLink(UniversityOfferingMajor link) => db.UniversityOfferingMajors.Add(link);

    public void RemoveMajorLink(UniversityOfferingMajor link) => db.UniversityOfferingMajors.Remove(link);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<Media>> GetUniversityMediaAsync(Guid universityId, CancellationToken ct = default) =>
        await db.Media.AsNoTracking()
            .Where(m => m.EntityType == "University" && m.EntityId == universityId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);

    public async Task SaveUniversityMediaAsync(Guid universityId, string? logoUrl, string? coverUrl, CancellationToken ct = default)
    {
        var existing = await db.Media
            .Where(m => m.EntityType == "University" && m.EntityId == universityId && (m.Type == "logo" || m.Type == "cover"))
            .ToListAsync(ct);

        var logo = existing.FirstOrDefault(m => m.Type == "logo");
        if (logo is not null)
        {
            if (string.IsNullOrWhiteSpace(logoUrl))
            {
                db.Media.Remove(logo);
            }
            else
            {
                logo.Url = logoUrl.Trim();
            }
        }
        else if (!string.IsNullOrWhiteSpace(logoUrl))
        {
            db.Media.Add(new Media
            {
                EntityType = "University",
                EntityId = universityId,
                Type = "logo",
                Url = logoUrl.Trim(),
                DisplayOrder = 0
            });
        }

        var cover = existing.FirstOrDefault(m => m.Type == "cover");
        if (cover is not null)
        {
            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                db.Media.Remove(cover);
            }
            else
            {
                cover.Url = coverUrl.Trim();
            }
        }
        else if (!string.IsNullOrWhiteSpace(coverUrl))
        {
            db.Media.Add(new Media
            {
                EntityType = "University",
                EntityId = universityId,
                Type = "cover",
                Url = coverUrl.Trim(),
                DisplayOrder = 0
            });
        }
    }
}
