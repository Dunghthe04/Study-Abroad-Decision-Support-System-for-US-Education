using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Admin.ChatReview;
using StudyAbroad.Application.Admin.ReviewBatches;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ReviewBatchRepository(AppDbContext db) : IReviewBatchRepository
{
    public async Task<IReadOnlyList<ReviewCandidate>> ListCandidatesAsync(
        DateTime? fromUtc, DateTime? toUtcExclusive, CancellationToken ct = default) =>
        await ReviewCandidateQuery.Candidates(db.ChatMessages.AsNoTracking(), fromUtc, toUtcExclusive).ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> FindActiveAdminIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id) && u.Role == UserRoles.Admin && u.Status == UserStatuses.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task AddAsync(
        ReviewBatch batch, IReadOnlyList<ReviewBatchItem> items, IReadOnlyList<ReviewBatchReviewer> reviewers, AuditLog audit,
        CancellationToken ct = default)
    {
        db.ReviewBatches.Add(batch);
        db.ReviewBatchItems.AddRange(items);
        db.ReviewBatchReviewers.AddRange(reviewers);
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);   // một lần lưu: đợt, các câu, người được giao và nhật ký
    }

    public async Task<IReadOnlyList<ReviewBatchListRow>> ListForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var batches = await db.ReviewBatches.AsNoTracking()
            .Where(b => b.CreatedByUserId == userId
                        || db.ReviewBatchReviewers.Any(r => r.ReviewBatchId == b.Id && r.UserId == userId))
            .OrderByDescending(b => b.CreatedAt)
            .ThenBy(b => b.Id)
            .ToListAsync(ct);
        if (batches.Count == 0)
            return [];

        var ids = batches.Select(b => b.Id).ToList();
        var totals = await db.ReviewBatchItems.AsNoTracking()
            .Where(i => ids.Contains(i.ReviewBatchId))
            .GroupBy(i => i.ReviewBatchId)
            .Select(g => new { BatchId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var reviewers = await (
                from r in db.ReviewBatchReviewers.AsNoTracking()
                where ids.Contains(r.ReviewBatchId)
                join u in db.Users.AsNoTracking() on r.UserId equals u.Id
                select new { r.ReviewBatchId, r.UserId, u.FullName })
            .ToListAsync(ct);
        var graded = await (
                from v in db.ReviewVerdicts.AsNoTracking()
                join i in db.ReviewBatchItems.AsNoTracking() on v.ReviewBatchItemId equals i.Id
                where ids.Contains(i.ReviewBatchId)
                group v by new { i.ReviewBatchId, v.ReviewerUserId } into g
                select new { g.Key.ReviewBatchId, g.Key.ReviewerUserId, Count = g.Count() })
            .ToListAsync(ct);

        return batches.Select(b => new ReviewBatchListRow(
                b.Id, b.Name, b.Seed, b.CreatedAt, b.SizePerStratum, b.SampledFromUtc, b.SampledToUtcExclusive, b.StrataJson,
                totals.FirstOrDefault(t => t.BatchId == b.Id)?.Count ?? 0,
                b.CreatedByUserId == userId,
                reviewers.Any(r => r.ReviewBatchId == b.Id && r.UserId == userId),
                reviewers.Where(r => r.ReviewBatchId == b.Id)
                    .Select(r => new ReviewerProgressRow(
                        r.UserId, r.FullName,
                        graded.FirstOrDefault(g => g.ReviewBatchId == b.Id && g.ReviewerUserId == r.UserId)?.Count ?? 0))
                    .ToList()))
            .ToList();
    }

    public async Task<ReviewItemsRows?> ListItemsAsync(Guid batchId, Guid reviewerUserId, CancellationToken ct = default)
    {
        var batch = await db.ReviewBatches.AsNoTracking()
            .Where(b => b.Id == batchId && db.ReviewBatchReviewers.Any(r => r.ReviewBatchId == b.Id && r.UserId == reviewerUserId))
            .Select(b => new { b.Id, b.Name })
            .FirstOrDefaultAsync(ct);
        if (batch is null)
            return null;

        var items = await db.ReviewBatchItems.AsNoTracking()
            .Where(i => i.ReviewBatchId == batchId)
            .OrderBy(i => i.Position)
            .Select(i => new ReviewItemRow(
                i.Id, i.Position, i.Stratum,
                db.ReviewVerdicts.Any(v => v.ReviewBatchItemId == i.Id && v.ReviewerUserId == reviewerUserId)))
            .ToListAsync(ct);
        return new ReviewItemsRows(batch.Id, batch.Name, items);
    }

    public async Task<ReviewItemDetailRows?> FindItemDetailAsync(
        Guid batchId, Guid itemId, Guid reviewerUserId, int contextCount, CancellationToken ct = default)
    {
        // Phải được giao đúng đợt này và câu phải thuộc đợt này; không thì như không có gì
        var item = await db.ReviewBatchItems.AsNoTracking()
            .Where(i => i.Id == itemId && i.ReviewBatchId == batchId
                        && db.ReviewBatchReviewers.Any(r => r.ReviewBatchId == batchId && r.UserId == reviewerUserId))
            .Select(i => new { i.Id, i.Position, i.Stratum, i.ChatMessageId })
            .FirstOrDefaultAsync(ct);
        if (item is null)
            return null;

        var messages = db.ChatMessages.AsNoTracking();
        var reply = await ReviewItemQuery.AnsweredReply(messages, db.ChatSessions.AsNoTracking(), item.ChatMessageId).FirstOrDefaultAsync(ct);
        if (reply is null)
            return null;

        var question = reply.QuestionMessageId is { } questionId
            ? await ChatReviewAggregator.QuestionDetail(messages, questionId).FirstOrDefaultAsync(ct)
            : null;
        // Ngữ cảnh là các tin trước câu hỏi (nếu không có tin hỏi thì trước chính tin trả lời)
        var context = await ChatReviewAggregator.ContextBefore(
                messages, reply.SessionId, question?.CreatedAt ?? reply.CreatedAt, question?.MessageId ?? reply.MessageId, contextCount)
            .ToListAsync(ct);
        var mine = await db.ReviewVerdicts.AsNoTracking()
            .FirstOrDefaultAsync(v => v.ReviewBatchItemId == itemId && v.ReviewerUserId == reviewerUserId, ct);
        return new ReviewItemDetailRows(item.Id, item.Position, item.Stratum, reply, question, context, mine);
    }

    public async Task<ReviewVerdict?> UpsertVerdictAsync(
        Guid batchId, Guid itemId, Guid reviewerUserId, string labelVerdict, string? correctIntent, string answerVerdict, string? cause,
        CancellationToken ct = default)
    {
        var allowed = await db.ReviewBatchItems.AnyAsync(
            i => i.Id == itemId && i.ReviewBatchId == batchId
                 && db.ReviewBatchReviewers.Any(r => r.ReviewBatchId == batchId && r.UserId == reviewerUserId), ct);
        if (!allowed)
            return null;

        var verdict = await db.ReviewVerdicts.FirstOrDefaultAsync(
            v => v.ReviewBatchItemId == itemId && v.ReviewerUserId == reviewerUserId, ct);
        if (verdict is null)
        {
            verdict = new ReviewVerdict { ReviewBatchItemId = itemId, ReviewerUserId = reviewerUserId };
            db.ReviewVerdicts.Add(verdict);
        }
        verdict.LabelVerdict = labelVerdict;
        verdict.CorrectIntent = correctIntent;
        verdict.AnswerVerdict = answerVerdict;
        verdict.Cause = cause;
        await db.SaveChangesAsync(ct);
        return verdict;
    }
}
