using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>[UC-92, UC-93] Yêu cầu xóa tài khoản trung tâm và thao tác xóa dữ liệu trung tâm.</summary>
public class CenterDeletionRepository(AppDbContext db) : ICenterDeletionRepository
{
    public const string DeletedCenterName = "Trung tâm đã xóa";
    public const string DeletedAccountName = "Tài khoản đã xóa";

    public async Task<CenterDeletionTarget?> GetTargetByOwnerAsync(Guid ownerUserId, CancellationToken ct = default)
    {
        var row = await (
            from c in db.StudyCenters.AsNoTracking()
            where c.OwnerUserId == ownerUserId
            join u in db.Users.AsNoTracking() on c.OwnerUserId equals u.Id
            select new { c.Id, c.Name, CenterEmail = c.Email, LoginEmail = u.Email })
            .FirstOrDefaultAsync(ct);

        return row is null
            ? null
            : new CenterDeletionTarget(row.Id, row.Name, string.IsNullOrWhiteSpace(row.CenterEmail) ? row.LoginEmail : row.CenterEmail);
    }

    public Task<CenterDeletionRequest?> GetLatestForUserAsync(Guid userId, CancellationToken ct = default) =>
        db.CenterDeletionRequests.AsNoTracking()
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<CenterDeletionRequest?> GetPendingForCenterAsync(Guid centerId, CancellationToken ct = default) =>
        db.CenterDeletionRequests
            .FirstOrDefaultAsync(r => r.StudyCenterId == centerId && r.Status == CenterDeletionService.StatusPending, ct);

    public Task<CenterDeletionRequest?> GetForUpdateAsync(Guid requestId, CancellationToken ct = default) =>
        db.CenterDeletionRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);

    public async Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default)
    {
        db.CenterDeletionRequests.Add(request);
        await db.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(CenterDeletionRequest request, CancellationToken ct = default)
    {
        request.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<CenterDeletionRequest>> SearchAsync(string status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.CenterDeletionRequests.AsNoTracking();
        if (status != "all")
            query = query.Where(r => r.Status == status);

        var total = await query.CountAsync(ct);
        // Hàng đợi chờ xử lý: gần hết hạn lên đầu. Các danh sách khác: mới nhất lên đầu.
        var ordered = status == CenterDeletionService.StatusPending
            ? query.OrderBy(r => r.DueAt).ThenBy(r => r.Id)
            : query.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id);

        var items = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<CenterDeletionRequest>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveAdminUserIdsAsync(CancellationToken ct = default) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == UserRoles.Admin && u.Status == UserStatuses.Active)
            .Select(u => u.Id)
            .ToListAsync(ct);

    public async Task<CenterDeletionOutcome> ExecuteDeletionAsync(Guid? centerId, Guid? ownerUserId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var affectedUsers = new HashSet<Guid>();
        var closedLeads = 0;

        if (centerId is { } id)
        {
            // 1. Đóng các yêu cầu liên hệ đang mở và ghi lại người cần thông báo
            var openLeads = await db.Leads.Where(l => l.StudyCenterId == id && l.Status != "closed").ToListAsync(ct);
            foreach (var lead in openLeads)
            {
                lead.Status = "closed";
                lead.UpdatedAt = DateTime.UtcNow;
                affectedUsers.Add(lead.UserId);
                if (lead.SubmittedByUserId is { } submitter) affectedUsers.Add(submitter);
            }
            closedLeads = openLeads.Count;

            // 2. Gỡ trung tâm khỏi danh bạ và xóa thông tin hiển thị, liên lạc, pháp lý
            var center = await db.StudyCenters.FirstOrDefaultAsync(c => c.Id == id, ct);
            if (center is not null)
                ScrubCenter(center);

            // 3. Hồ sơ xác minh (tài liệu pháp lý) và các thay đổi chờ kiểm tra không còn lý do lưu
            db.CenterVerificationRequests.RemoveRange(await db.CenterVerificationRequests.Where(r => r.CenterId == id).ToListAsync(ct));
            db.CenterProfileChanges.RemoveRange(await db.CenterProfileChanges.Where(c => c.StudyCenterId == id).ToListAsync(ct));
        }

        if (ownerUserId is { } userId)
        {
            // 4. Vô hiệu và ẩn danh tài khoản chủ trung tâm, thu hồi mọi phiên đăng nhập
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == UserRoles.Center, ct);
            if (user is not null)
            {
                ScrubUser(user);
                var sessions = await db.UserSessions.Where(s => s.UserId == userId && !s.IsRevoked).ToListAsync(ct);
                foreach (var session in sessions) session.IsRevoked = true;
                db.OtpTokens.RemoveRange(await db.OtpTokens.Where(o => o.UserId == userId).ToListAsync(ct));
            }
            affectedUsers.Remove(userId);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new CenterDeletionOutcome(closedLeads, affectedUsers.ToList());
    }

    /// <summary>Giữ lại dòng (để lead và đánh giá của người dùng không bị xóa dây chuyền) nhưng không còn dữ liệu của trung tâm.</summary>
    public static void ScrubCenter(StudyCenter center)
    {
        center.Name = DeletedCenterName;
        center.IsActive = false;
        center.VerificationStatus = CenterVerificationStatuses.Unverified;
        center.VerifiedAt = null;
        center.VerifiedByUserId = null;
        center.VerificationNote = null;
        center.OwnerUserId = null;
        center.Website = null;
        center.FanpageUrl = null;
        center.Address = null;
        center.City = null;
        center.District = null;
        center.Latitude = null;
        center.Longitude = null;
        center.Phone = null;
        center.Email = null;
        center.EmailVerifiedAt = null;
        center.PhoneVerifiedAt = null;
        center.Description = null;
        center.RegistrantName = null;
        center.RegistrantPosition = null;
        center.FeeMin = null;
        center.FeeMax = null;
        center.FeeDescription = null;
        center.LogoUrl = null;
        center.CoverImageUrl = null;
        center.GalleryImages = [];
        center.Services = [];
        center.StudyLevels = [];
        center.Accreditations = [];
        center.LegalDocumentUrls = [];
        center.StaffCount = null;
        center.EstablishedYear = null;
        center.LicenseNumber = null;
        center.TaxCode = null;
        center.LegalRepresentative = null;
        center.LicenseIssuedBy = null;
        center.LicenseIssuedDate = null;
        center.TrainingLicenseNumber = null;
        center.CertifiedCounselorCount = null;
        center.VisaSuccessRate = null;
        center.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Tài khoản không đăng nhập lại được: trạng thái "deleted", email và mật khẩu bị thay bằng giá trị không dùng được.</summary>
    public static void ScrubUser(User user)
    {
        user.Status = UserStatuses.Deleted;
        user.Email = $"deleted-{user.Id:N}@deleted.invalid";
        user.FullName = DeletedAccountName;
        user.Phone = null;
        user.DateOfBirth = null;
        user.PasswordHash = $"deleted:{Guid.NewGuid():N}";
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.UpdatedAt = DateTime.UtcNow;
    }
}
