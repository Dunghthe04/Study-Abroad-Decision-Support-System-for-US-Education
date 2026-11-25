using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class StudyCenterRepository(AppDbContext db) : IStudyCenterRepository
{
    public async Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
    {
        var q = db.StudyCenters.AsNoTracking().AsQueryable();
        if (query.OnlyInactive)
            q = q.Where(x => !x.IsActive);
        else if (!query.IncludeInactive)
            q = q.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(query.CenterType))
            q = q.Where(x => x.CenterType == query.CenterType.Trim());

        if (query.VerifiedOnly == true)
            q = q.Where(x => x.VerificationStatus == CenterReferral.Verified);
        else if (!string.IsNullOrWhiteSpace(query.VerificationStatus))
            q = q.Where(x => x.VerificationStatus.ToLower() == query.VerificationStatus.Trim().ToLower());

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(x => EF.Functions.ILike(x.Name, $"%{query.Search}%"));
        if (!string.IsNullOrWhiteSpace(query.City))
            q = q.Where(x => x.City != null && EF.Functions.ILike(x.City, query.City));
        if (!string.IsNullOrWhiteSpace(query.Service))
            q = q.Where(x => x.Services.Contains(query.Service));
        if (!string.IsNullOrWhiteSpace(query.StudyLevel))
            q = q.Where(x => x.StudyLevels.Contains(query.StudyLevel));

        var total = await q.CountAsync(ct);
        // Danh sách quản trị (moderator): hồ sơ chờ duyệt lên đầu, rồi chưa xác minh, mỗi nhóm mới nhất trước,
        // để trung tâm vừa đăng ký không bị chìm ở các trang sau. Danh bạ công khai vẫn xếp theo tên.
        var ordered = query.IncludeInactive || query.OnlyInactive
            ? q.OrderBy(x => x.VerificationStatus == "pending_review" ? 0 : x.VerificationStatus == "unverified" ? 1 : 2)
                .ThenByDescending(x => x.CreatedAt)
            : q.OrderBy(x => x.Name);

        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        await ApplyLiveReviewStatsAsync(items, ct);

        return new PagedResult<StudyCenter>(items, query.Page, query.PageSize, total);
    }

    public async Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default)
    {
        var centers = await db.StudyCenters.AsNoTracking()
            .Where(x => x.IsActive && x.VerificationStatus == CenterReferral.Verified)
            .ToListAsync(ct);
        await ApplyLiveReviewStatsAsync(centers, ct);
        return centers;
    }

    public async Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var center = await db.StudyCenters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (center is not null)
            await ApplyLiveReviewStatsAsync([center], ct);
        return center;
    }

    /// <summary>
    /// Số đánh giá và điểm trung bình luôn tính từ bảng CenterReviews (chỉ đánh giá đang hiển thị),
    /// không dùng hai cột lưu sẵn. Chỉ áp lên thực thể không theo dõi (AsNoTracking) nên không ghi ngược vào DB.
    /// Trung tâm chưa có đánh giá thì ReviewCount = 0 và AverageRating = 0.
    /// </summary>
    private async Task ApplyLiveReviewStatsAsync(IReadOnlyCollection<StudyCenter> centers, CancellationToken ct)
    {
        if (centers.Count == 0) return;

        var ids = centers.Select(c => c.Id).ToList();
        var stats = await db.CenterReviews.AsNoTracking()
            .Where(r => r.Status == "visible" && ids.Contains(r.StudyCenterId))
            .GroupBy(r => r.StudyCenterId)
            .Select(g => new { Id = g.Key, Count = g.Count(), Average = g.Average(r => (double)r.Rating) })
            .ToListAsync(ct);
        var byCenter = stats.ToDictionary(x => x.Id);

        foreach (var center in centers)
        {
            if (byCenter.TryGetValue(center.Id, out var s))
            {
                center.ReviewCount = s.Count;
                center.AverageRating = (decimal)CenterRatingMath.Round(s.Average);
            }
            else
            {
                center.ReviewCount = 0;
                center.AverageRating = 0m;
            }
        }
    }

    public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
        db.StudyCenters.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
        db.StudyCenters.AnyAsync(x => x.Code.ToLower() == code.ToLower(), ct);

    public async Task AddAsync(StudyCenter center, CancellationToken ct = default)
    {
        await db.StudyCenters.AddAsync(center, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(StudyCenter center, CancellationToken ct = default)
    {
        center.UpdatedAt = DateTime.UtcNow;
        db.StudyCenters.Update(center);
        await db.SaveChangesAsync(ct);
    }

    public async Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default)
    {
        var center = await db.StudyCenters.FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId, ct);
        if (center is not null) return center;

        if (!string.IsNullOrWhiteSpace(email))
        {
            var normalized = email.Trim().ToLower();
            center = await db.StudyCenters.FirstOrDefaultAsync(x => x.Email != null && x.Email.ToLower() == normalized, ct);
            if (center is not null && center.OwnerUserId is null)
            {
                center.OwnerUserId = ownerUserId;
                center.UpdatedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }
        }
        return center;
    }

    public async Task<Dictionary<Guid, CenterResponseMetrics>> GetResponseMetrics90dAsync(
        IReadOnlyCollection<Guid> centerIds,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, CenterResponseMetrics>();
        if (centerIds.Count == 0) return result;

        var cutoff = DateTime.UtcNow.AddDays(-90);
        var ids = centerIds.Distinct().ToList();

        var recentLeads = await db.Leads.AsNoTracking()
            .Where(l => ids.Contains(l.StudyCenterId)
                && l.CreatedAt >= cutoff
                && l.Status != LeadStatuses.PendingParent
                && l.Status != LeadStatuses.Cancelled)
            .Select(l => new { l.Id, l.StudyCenterId, l.Status, l.CreatedAt, l.UpdatedAt })
            .ToListAsync(ct);

        if (recentLeads.Count == 0)
            return result;

        var leadIds = recentLeads.Select(l => l.Id).ToList();
        var staffRoles = new[] { UserRoles.Center, UserRoles.Moderator, UserRoles.Admin };
        var firstCenterMessages = await (
                from m in db.LeadMessages.AsNoTracking()
                join u in db.Users.AsNoTracking() on m.SenderUserId equals u.Id
                where leadIds.Contains(m.LeadId) && staffRoles.Contains(u.Role)
                group m by m.LeadId into g
                select new { LeadId = g.Key, FirstAt = g.Min(x => x.CreatedAt) })
            .ToDictionaryAsync(x => x.LeadId, x => x.FirstAt, ct);

        foreach (var group in recentLeads.GroupBy(l => l.StudyCenterId))
        {
            var items = group.ToList();
            var sampleCount = items.Count;
            if (sampleCount < 5)
            {
                result[group.Key] = new CenterResponseMetrics(null, null, sampleCount);
                continue;
            }

            var responseHours = new List<double>();
            var respondedCount = 0;
            foreach (var lead in items)
            {
                var hasCenterMessage = firstCenterMessages.TryGetValue(lead.Id, out var firstMsgAt);
                var statusResponded = lead.Status is LeadStatuses.Accepted
                    or LeadStatuses.Contacted
                    or LeadStatuses.Rejected
                    or LeadStatuses.Closed;

                if (!hasCenterMessage && !statusResponded)
                    continue;

                respondedCount++;
                DateTime respondedAt;
                if (hasCenterMessage && statusResponded && lead.UpdatedAt is { } updatedAt && updatedAt > lead.CreatedAt)
                    respondedAt = firstMsgAt < updatedAt ? firstMsgAt : updatedAt;
                else if (hasCenterMessage)
                    respondedAt = firstMsgAt;
                else
                    respondedAt = lead.UpdatedAt ?? lead.CreatedAt;

                var hours = Math.Max(0, (respondedAt - lead.CreatedAt).TotalHours);
                responseHours.Add(hours);
            }

            double? medianHours = null;
            if (responseHours.Count > 0)
            {
                responseHours.Sort();
                var mid = responseHours.Count / 2;
                var rawMedian = responseHours.Count % 2 == 1
                    ? responseHours[mid]
                    : (responseHours[mid - 1] + responseHours[mid]) / 2.0;
                medianHours = Math.Round(rawMedian, 1, MidpointRounding.AwayFromZero);
            }

            var ratePercent = Math.Round(100.0 * respondedCount / sampleCount, 1, MidpointRounding.AwayFromZero);
            result[group.Key] = new CenterResponseMetrics(medianHours, ratePercent, sampleCount);
        }

        return result;
    }

    public async Task<CenterGuidanceCardsDto> GetGuidanceCardsAsync(CancellationToken ct = default)
    {
        var docs = await db.KnowledgeDocuments.AsNoTracking()
            .Where(d => d.TopicCode != null
                && (d.TopicCode == "CENTER_ASK" || d.TopicCode == "CENTER_WARNING"))
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

        static CenterGuidanceCardDto? Pick(List<KnowledgeDocument> list, string topicCode, string preferredDocType)
        {
            var doc = list.FirstOrDefault(d =>
                    string.Equals(d.TopicCode, topicCode, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(d.DocType, preferredDocType, StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(d =>
                    string.Equals(d.TopicCode, topicCode, StringComparison.OrdinalIgnoreCase));
            if (doc is null || string.IsNullOrWhiteSpace(doc.Answer)) return null;
            return new CenterGuidanceCardDto(
                doc.TopicCode ?? topicCode,
                doc.DocType,
                doc.Question,
                doc.Answer,
                doc.Source,
                doc.SourceUrl);
        }

        return new CenterGuidanceCardsDto(
            Pick(docs, "CENTER_ASK", "proc_checklist"),
            Pick(docs, "CENTER_WARNING", "proc_mistakes"));
    }
}
