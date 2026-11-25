using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-151 / UC-53] Lưu và truy vấn báo cáo đánh giá trung tâm.</summary>
public class CenterReviewReportRepository(AppDbContext db) : ICenterReviewReportRepository
{
    public Task<CenterReview?> GetVisibleReviewAsync(Guid centerId, Guid reviewId, CancellationToken ct = default) =>
        db.CenterReviews.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.StudyCenterId == centerId && r.Status == "visible", ct);

    public Task<bool> HasOpenReportAsync(Guid reviewId, Guid reporterUserId, CancellationToken ct = default) =>
        db.CenterReviewReports.AsNoTracking()
            .AnyAsync(r => r.CenterReviewId == reviewId
                && r.ReporterUserId == reporterUserId
                && r.Status == CenterReviewReportStatuses.Open, ct);

    public async Task<bool> TryAddAsync(CenterReviewReport report, CancellationToken ct = default)
    {
        db.CenterReviewReports.Add(report);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(report).State = EntityState.Detached;
            return false;
        }
    }

    public async Task<PagedResult<CenterReviewReportDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 50 ? 20 : pageSize;

        var query = from rp in db.CenterReviewReports.AsNoTracking()
                    join rev in db.CenterReviews.AsNoTracking() on rp.CenterReviewId equals rev.Id
                    join c in db.StudyCenters.AsNoTracking() on rp.StudyCenterId equals c.Id
                    join reporter in db.Users.AsNoTracking() on rp.ReporterUserId equals reporter.Id
                    join reviewer in db.Users.AsNoTracking() on rev.UserId equals reviewer.Id into reviewerJoin
                    from reviewer in reviewerJoin.DefaultIfEmpty()
                    select new
                    {
                        Report = rp,
                        Review = rev,
                        CenterName = c.Name,
                        ReporterName = reporter.FullName,
                        ReviewerFullName = reviewer != null ? reviewer.FullName : null,
                        ReviewerStatus = reviewer != null ? reviewer.Status : null,
                    };

        query = status switch
        {
            "resolved" => query.Where(x => x.Report.Status != CenterReviewReportStatuses.Open),
            "all" => query,
            _ => query.Where(x => x.Report.Status == CenterReviewReportStatuses.Open),
        };

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.Report.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .ToListAsync(ct);

        var items = rows.Select(x => new CenterReviewReportDto(
            x.Report.Id,
            x.Report.CenterReviewId,
            x.Report.StudyCenterId,
            x.CenterName,
            x.Review.Rating,
            x.Review.Comment,
            x.Review.Status,
            CenterReviewRepository.FormatReviewerName(x.ReviewerFullName, x.ReviewerStatus),
            x.Report.ReporterUserId,
            x.ReporterName,
            x.Report.ReporterRole,
            x.Report.IsCenterSelfReport,
            x.Report.Reason,
            x.Report.Detail,
            x.Report.Status,
            x.Report.CreatedAt,
            x.Report.ResolvedByUserId,
            x.Report.ResolvedAt,
            x.Report.ResolutionNote)).ToList();

        return new PagedResult<CenterReviewReportDto>(items, safePage, safeSize, total);
    }

    public Task<CenterReviewReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default) =>
        db.CenterReviewReports.FirstOrDefaultAsync(r => r.Id == reportId, ct);

    public async Task UpdateAsync(CenterReviewReport report, CancellationToken ct = default)
    {
        report.UpdatedAt = DateTime.UtcNow;
        db.CenterReviewReports.Update(report);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResolveOpenReportsForRemovedReviewAsync(
        Guid reviewId,
        Guid moderatorUserId,
        string? reason,
        DateTime now,
        CancellationToken ct = default)
    {
        var open = await db.CenterReviewReports
            .Where(r => r.CenterReviewId == reviewId && r.Status == CenterReviewReportStatuses.Open)
            .ToListAsync(ct);
        if (open.Count == 0) return;

        foreach (var rp in open)
        {
            rp.Status = CenterReviewReportStatuses.ResolvedRemoved;
            rp.ResolvedByUserId = moderatorUserId;
            rp.ResolvedAt = now;
            rp.ResolutionNote = reason;
            rp.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);
    }
}
