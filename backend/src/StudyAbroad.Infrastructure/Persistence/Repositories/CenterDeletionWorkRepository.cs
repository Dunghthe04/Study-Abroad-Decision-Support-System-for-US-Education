using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-93] Câu lệnh dữ liệu cho việc Administrator xử lý và thực hiện xóa trung tâm.</summary>
public class CenterDeletionWorkRepository(AppDbContext db, IHostEnvironment? env = null) : ICenterDeletionWorkRepository
{
    public const string PurgedCenterName = "Trung tâm đã xóa";
    public const string DeletedAccountName = "Tài khoản đã xóa";
    public const string ErasedMessageContent = "Tin nhắn này đã được xóa theo yêu cầu xóa dữ liệu của trung tâm.";

    /// <summary>Lead còn mở phía trung tâm: chưa đóng, chưa thu hồi. "accepted" là trạng thái của UC-73.</summary>
    private static readonly string[] OpenStatuses = [LeadStatuses.New, LeadStatuses.Contacted, "accepted"];

    public async Task<PagedResult<AdminCenterDeletionRequestDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var safePage = page < 1 ? 1 : page;
        var safeSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var q = db.CenterDeletionRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(x => x.Status == status);

        var total = await q.CountAsync(ct);
        var ids = await q
            .OrderBy(x => x.Status == CenterDeletionRequestStatuses.Pending ? 0 : 1)
            .ThenBy(x => x.CreatedAt)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var views = await ProjectAsync(ids, ct);
        var ordered = ids.Select(id => views.First(v => v.Id == id)).ToList();
        return new PagedResult<AdminCenterDeletionRequestDto>(ordered, safePage, safeSize, total);
    }

    public async Task<AdminCenterDeletionRequestDto?> GetViewAsync(Guid id, CancellationToken ct = default) =>
        (await ProjectAsync([id], ct)).FirstOrDefault();

    private async Task<List<AdminCenterDeletionRequestDto>> ProjectAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        var rows = await (from r in db.CenterDeletionRequests.AsNoTracking()
                          where ids.Contains(r.Id)
                          join c in db.StudyCenters.AsNoTracking() on r.StudyCenterId equals c.Id
                          join u in db.Users.AsNoTracking() on r.RequestedByUserId equals u.Id into requesters
                          from u in requesters.DefaultIfEmpty()
                          join o in db.Users.AsNoTracking() on c.OwnerUserId equals o.Id into owners
                          from o in owners.DefaultIfEmpty()
                          select new
                          {
                              Request = r,
                              Center = c,
                              RequesterName = u != null ? u.FullName : null,
                              RequesterEmail = u != null ? u.Email : null,
                              OwnerHold = o != null && o.IsLegalHold,
                              OwnerHoldReason = o != null ? o.LegalHoldReason : null,
                              OpenReports = db.LeadMessages.Count(m => m.ReportedAt != null && m.ReportResolution == null
                                  && db.Leads.Any(l => l.Id == m.LeadId && l.StudyCenterId == c.Id)),
                          })
            .ToListAsync(ct);

        return rows.Select(x => new AdminCenterDeletionRequestDto(
            x.Request.Id,
            x.Center.Id,
            x.Center.Name,
            x.Center.Email,
            x.Center.Phone,
            x.Center.VerificationStatus,
            x.Request.RequestedByUserId,
            x.RequesterName,
            x.RequesterEmail,
            x.Request.RequestedByUserId is not null && x.Request.RequestedByUserId == x.Center.OwnerUserId,
            x.Request.Reason,
            x.Request.Status,
            x.Request.CreatedAt,
            x.Request.HandledByUserId,
            x.Request.HandledAt,
            x.Request.DecisionNote,
            x.Request.ScheduledPurgeAt,
            x.Request.CompletedAt,
            x.Center.IsLegalHold || x.OwnerHold,
            x.Center.LegalHoldReason ?? x.OwnerHoldReason,
            x.Center.LegalHoldAt,
            x.OpenReports,
            CenterDeletionDeadlines.DueAt(x.Request),
            x.Request.ExtendedAt,
            x.Request.ExtensionNote)).ToList();
    }

    /// <summary>Tin nhắn trong các lead của trung tâm bị học sinh/phụ huynh báo cáo mà Admin chưa xử lý (UC-58).</summary>
    public Task<int> CountOpenReportsAsync(Guid studyCenterId, CancellationToken ct = default) =>
        db.LeadMessages.CountAsync(m => m.ReportedAt != null && m.ReportResolution == null
            && db.Leads.Any(l => l.Id == m.LeadId && l.StudyCenterId == studyCenterId), ct);

    public async Task<bool> LockCenterIfPurgeableAsync(Guid studyCenterId, CancellationToken ct = default)
    {
        var ids = await db.Database.SqlQuery<Guid>($"""
            SELECT c.id AS "Value" FROM app.study_centers c
            LEFT JOIN app.users o ON o.id = c.owner_user_id
            WHERE c.id = {studyCenterId}
              AND c.is_legal_hold = FALSE
              AND COALESCE(o.is_legal_hold, FALSE) = FALSE
            FOR UPDATE OF c
            """).ToListAsync(ct);
        return ids.Count > 0;
    }

    public async Task AnonymizeOwnerAccountAsync(Guid userId, DateTime now, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        // Không xóa dòng: lead_messages.sender_user_id cascade theo user nên xóa sẽ mất tin nhắn trung tâm đã gửi,
        // gồm cả tin bị báo cáo. Giữ dòng nhưng bỏ hết thông tin nhận diện và không thể đăng nhập lại.
        user.Email = $"deleted-{user.Id:N}@deleted.invalid";
        user.PasswordHash = string.Empty;
        user.FullName = DeletedAccountName;
        user.Phone = null;
        user.DateOfBirth = null;
        user.Status = UserStatuses.Deleted;
        user.DeletedAt ??= now;
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.IsLegalHold = false;
        user.LegalHoldReason = null;
        user.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await db.UserSessions.Where(s => s.UserId == userId).ExecuteDeleteAsync(ct);
        await db.OtpTokens.Where(t => t.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Notifications.Where(n => n.UserId == userId).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<ClosedLeadRecipients>> CloseOpenLeadsAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default)
    {
        var leads = await db.Leads
            .Where(l => l.StudyCenterId == studyCenterId
                && (OpenStatuses.Contains(l.Status) || l.Status == LeadStatuses.PendingParent))
            .ToListAsync(ct);

        foreach (var lead in leads)
        {
            // Lead chờ phụ huynh xác nhận thì trung tâm chưa nhận: hủy thay vì đóng
            lead.Status = lead.Status == LeadStatuses.PendingParent ? LeadStatuses.Cancelled : LeadStatuses.Closed;
            lead.UpdatedAt = now;
        }
        await db.SaveChangesAsync(ct);

        return leads.Select(l => new ClosedLeadRecipients(l.Id, l.UserId, l.SubmittedByUserId)).ToList();
    }

    public async Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
    {
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);
    }

    public Task<List<Guid>> GetDuePurgeRequestIdsAsync(DateTime now, CancellationToken ct = default) =>
        (from r in db.CenterDeletionRequests.AsNoTracking()
         join c in db.StudyCenters.AsNoTracking() on r.StudyCenterId equals c.Id
         where r.Status == CenterDeletionRequestStatuses.Approved
            && r.ScheduledPurgeAt != null && r.ScheduledPurgeAt <= now
            && !c.IsLegalHold
            && !db.Users.Any(o => o.Id == c.OwnerUserId && o.IsLegalHold)
         orderby r.ScheduledPurgeAt
         select r.Id).ToListAsync(ct);

    public async Task PurgeCenterDataAsync(Guid studyCenterId, DateTime now, CancellationToken ct = default)
    {
        // Hồ sơ xác minh (tài liệu xóa theo cascade) và lịch sử sửa hồ sơ thuộc về trung tâm
        await db.CenterVerificationRequests.Where(r => r.CenterId == studyCenterId).ExecuteDeleteAsync(ct);
        await db.CenterProfileChanges.Where(c => c.StudyCenterId == studyCenterId).ExecuteDeleteAsync(ct);

        // Giữ dòng trung tâm để lead và đánh giá cũ của người dùng còn tham chiếu, nhưng bỏ hết thông tin nhận diện
        var center = await db.StudyCenters.FirstOrDefaultAsync(c => c.Id == studyCenterId, ct);
        if (center is null) return;

        center.Name = PurgedCenterName;
        center.Code = center.Id.ToString("N");   // mã gốc có thể nhận diện; cột tối đa 32 ký tự, unique
        center.Website = null;
        center.Fanpage = null;
        center.ServiceAreas = [];
        center.Branches = [];
        center.Address = null;
        center.City = null;
        center.District = null;
        center.Latitude = null;
        center.Longitude = null;
        center.Phone = null;
        center.Email = null;
        center.Description = null;
        center.EmailVerifiedAt = null;
        center.PhoneVerifiedAt = null;
        center.RegistrantName = null;
        center.RegistrantPosition = null;
        center.FeeMin = null;
        center.FeeMax = null;
        center.FeeDescription = null;
        center.VerificationStatus = CenterVerificationStatuses.Unverified;
        center.VerifiedAt = null;
        center.VerifiedByUserId = null;
        center.VerificationNote = null;
        center.OwnerUserId = null;
        center.LogoUrl = null;
        center.CoverImageUrl = null;
        center.GalleryImages = [];
        center.StaffCount = null;
        center.EstablishedYear = null;
        center.LicenseNumber = null;
        center.TaxCode = null;
        center.LegalRepresentative = null;
        center.LicenseIssuedBy = null;
        center.LicenseIssuedDate = null;
        center.TrainingLicenseNumber = null;
        center.CertifiedCounselorCount = null;
        center.LegalDocumentUrls = [];
        center.Accreditations = [];
        center.IsActive = false;
        center.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
    }

    public async Task<ErasedLeadMessages> ErasePrivateLeadMessagesAsync(
        Guid studyCenterId, Guid? ownerUserId, DateTime now, CancellationToken ct = default)
    {
        if (ownerUserId is not { } owner) return new ErasedLeadMessages([], 0);

        // Tin phía trung tâm = tin do chủ tài khoản trung tâm gửi. Giữ tin đang bị báo cáo mà Admin chưa xử lý (UC-58).
        var centerSide = db.LeadMessages.Where(m =>
            m.SenderUserId == owner
            && m.Content != ErasedMessageContent
            && db.Leads.Any(l => l.Id == m.LeadId && l.StudyCenterId == studyCenterId));
        var kept = await centerSide.CountAsync(m => m.ReportedAt != null && m.ReportResolution == null, ct);
        var erasable = centerSide.Where(m => !(m.ReportedAt != null && m.ReportResolution == null));

        var attachments = await erasable
            .Where(m => m.AttachmentUrl != null)
            .Select(m => m.AttachmentUrl!)
            .ToListAsync(ct);

        await erasable.ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Content, ErasedMessageContent)
            .SetProperty(m => m.AttachmentUrl, (string?)null)
            .SetProperty(m => m.AttachmentPublicId, (string?)null)
            .SetProperty(m => m.FlagReason, (string?)null)
            .SetProperty(m => m.UpdatedAt, now), ct);

        return new ErasedLeadMessages(attachments.Distinct(StringComparer.Ordinal).ToList(), kept);
    }

    public async Task<IReadOnlyList<string>> GetCenterMediaUrlsAsync(Guid studyCenterId, CancellationToken ct = default)
    {
        var center = await db.StudyCenters.AsNoTracking()
            .Where(c => c.Id == studyCenterId)
            .Select(c => new { c.LogoUrl, c.CoverImageUrl, c.GalleryImages, c.LegalDocumentUrls })
            .FirstOrDefaultAsync(ct);
        if (center is null) return [];

        return new[] { center.LogoUrl, center.CoverImageUrl }
            .Concat(center.GalleryImages)
            .Concat(center.LegalDocumentUrls)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public Task DeleteCenterFilesAsync(Guid studyCenterId, CancellationToken ct = default)
    {
        if (env is null) return Task.CompletedTask;
        // Cùng thư mục CenterPortalController lưu tài liệu xác minh (UC-71)
        var folder = Path.Combine(env.ContentRootPath, "uploads", "verifications", studyCenterId.ToString());
        if (Directory.Exists(folder))
            Directory.Delete(folder, recursive: true);
        return Task.CompletedTask;
    }
}
