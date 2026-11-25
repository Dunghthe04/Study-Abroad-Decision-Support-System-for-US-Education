using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Notifications;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// Unit tests cho [UC-151] Báo cáo đánh giá trung tâm, [UC-152] Phản hồi đánh giá trung tâm,
/// và [UC-53 2b] Moderator gỡ phản hồi / tự đóng báo cáo khi gỡ đánh giá.
/// </summary>
public class CenterReviewInteractionsTests
{
    private sealed class FakeReportRepo : ICenterReviewReportRepository
    {
        public List<CenterReview> Reviews { get; } = [];
        public List<CenterReviewReport> Reports { get; } = [];
        public int ResolvedByRemovalCount { get; private set; }

        public Task<CenterReview?> GetVisibleReviewAsync(Guid centerId, Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.Id == reviewId && r.StudyCenterId == centerId && r.Status == "visible"));

        public Task<bool> HasOpenReportAsync(Guid reviewId, Guid reporterUserId, CancellationToken ct = default) =>
            Task.FromResult(Reports.Any(r =>
                r.CenterReviewId == reviewId
                && r.ReporterUserId == reporterUserId
                && r.Status == CenterReviewReportStatuses.Open));

        public Task<bool> TryAddAsync(CenterReviewReport report, CancellationToken ct = default)
        {
            if (Reports.Any(r => r.CenterReviewId == report.CenterReviewId && r.ReporterUserId == report.ReporterUserId && r.Status == CenterReviewReportStatuses.Open))
                return Task.FromResult(false);
            Reports.Add(report);
            return Task.FromResult(true);
        }

        public Task<PagedResult<CenterReviewReportDto>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<CenterReviewReportDto>([], page, pageSize, 0));

        public Task<CenterReviewReport?> GetByIdAsync(Guid reportId, CancellationToken ct = default) =>
            Task.FromResult(Reports.FirstOrDefault(r => r.Id == reportId));

        public Task UpdateAsync(CenterReviewReport report, CancellationToken ct = default) => Task.CompletedTask;

        public Task ResolveOpenReportsForRemovedReviewAsync(Guid reviewId, Guid moderatorUserId, string? reason, DateTime now, CancellationToken ct = default)
        {
            foreach (var r in Reports.Where(x => x.CenterReviewId == reviewId && x.Status == CenterReviewReportStatuses.Open))
            {
                r.Status = CenterReviewReportStatuses.ResolvedRemoved;
                r.ResolvedByUserId = moderatorUserId;
                r.ResolvedAt = now;
                r.ResolutionNote = reason;
                ResolvedByRemovalCount++;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCenterRepo : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];
        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));
        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) => GetByIdAsync(id, ct);
        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);
        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(StudyCenter center, CancellationToken ct = default) { Centers.Add(center); return Task.CompletedTask; }
        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) => Task.CompletedTask;
        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId || (email != null && c.Email == email)));
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public List<User> Users { get; } = [];
        public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> PhoneExistAsync(string phone, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task AddAsync(User user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateUserAsync(User user, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RevokeSessionAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RevokeOtherSessionsAsync(Guid userId, string keepTokenHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<UserSession?> GetSessionByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> RotateRefreshTokenAsync(Guid sessionId, string oldRefreshHash, string newRefreshHash, string newAccessHash, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task RevokeSessionByIdAsync(Guid sessionId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<bool> HasActiveSessionAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task SaveOtpAsync(OtpToken token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task UpdateOtpAsync(OtpToken token, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<OtpToken?> GetLatestOtpAsync(Guid userId, string purpose, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task InvalidateOtpsAsync(Guid userId, string purpose, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CountOtpRequestsInLastHourAsync(Guid userId, string purpose, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<int> CleanupExpiredOtpsAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<PagedResult<User>> GetAdminUserAccountListAsync(AdminUserAccountListQuery query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    private sealed class FakeParentLinks : IParentLinkRepository
    {
        public HashSet<Guid> ConsentedStudents { get; } = [];
        public Task<bool> HasGrantedConsentAsync(Guid studentUserId, CancellationToken ct = default) =>
            Task.FromResult(ConsentedStudents.Contains(studentUserId));
        public Task AddAsync(ParentStudentLink link, CancellationToken ct = default) => throw new NotImplementedException();
        public Task UpdateAsync(ParentStudentLink link, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ParentStudentLink?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ParentStudentLink?> GetByRevokeTokenHashAsync(string revokeTokenHash, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<ParentStudentLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ParentStudentLink>> GetActiveByParentAsync(Guid parentUserId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ParentStudentLink>> GetActiveWithoutParentAccountAsync(string parentEmail, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Consent?> GetConsentAsync(Guid consentId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveAcceptAsync(ParentStudentLink link, Consent consent, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveRevokeAsync(ParentStudentLink link, Consent? consent, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeAudit : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default) { Logs.Add(entry); return Task.CompletedTask; }
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
    }

    private sealed class FakeModerationRepo : ICenterReviewModerationRepository, ICenterReviewRepository, ICenterReviewSubmissionRepository
    {
        public List<CenterReview> Reviews { get; } = [];

        public Task<CenterReview?> GetForUpdateAsync(Guid centerId, Guid reviewId, CancellationToken ct = default) =>
            Task.FromResult(Reviews.FirstOrDefault(r => r.Id == reviewId && r.StudyCenterId == centerId));

        public Task SaveAsync(CenterReview review, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CenterReviewsDto> GetVisibleAsync(Guid centerId, int page, int pageSize, CancellationToken ct = default)
        {
            var visible = Reviews.Where(r => r.StudyCenterId == centerId && r.Status == "visible").ToList();
            return Task.FromResult(new CenterReviewsDto(
                visible.Count,
                visible.Count > 0 ? Math.Round(visible.Average(r => r.Rating), 2) : null,
                visible.Select(ToDto).ToList()));
        }

        public Task<CenterReviewDto?> GetPublicDtoAsync(Guid reviewId, CancellationToken ct = default)
        {
            var r = Reviews.FirstOrDefault(x => x.Id == reviewId);
            return Task.FromResult(r is null ? null : ToDto(r));
        }

        public Task<Lead?> FindLatestLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Lead?> FindLatestAcceptedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Lead?> FindLatestUnreviewedLeadAsync(Guid userId, Guid centerId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> TryAddAsync(CenterReview review, CancellationToken ct = default) => throw new NotImplementedException();

        private static CenterReviewDto ToDto(CenterReview r) =>
            new(r.Id, "Nguyễn Văn A.", "student", r.IsVerified, r.CreatedAt, r.Rating, r.Comment,
                r.CenterReplyStatus == "visible" && r.CenterReplyContent is not null && r.CenterReplyCreatedAt.HasValue
                    ? new CenterReviewReplyDto(r.CenterReplyContent, r.CenterReplyCreatedAt.Value, r.CenterReplyEditedAt)
                    : null);
    }

    private sealed class FakeNotifications : INotificationService
    {
        public List<(Guid UserId, string Type, string Title, string? Body, string? LinkUrl)> Sent { get; } = [];
        public Task NotifyAsync(Guid userId, string type, string title, string? body, string? linkUrl, CancellationToken ct = default)
        {
            Sent.Add((userId, type, title, body, linkUrl));
            return Task.CompletedTask;
        }
        public Task<PagedResult<NotificationDto>> GetMineAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> MarkReadAsync(Guid userId, Guid notificationId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> MarkAllReadAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private static StudyCenter VerifiedCenter(Guid ownerId, string name = "Trung tâm ABC") => new()
    {
        Code = "ABC",
        Name = name,
        OwnerUserId = ownerId,
        IsActive = true,
        VerificationStatus = CenterVerificationStatuses.Verified,
    };

    // ===== UC-151: Báo cáo đánh giá trung tâm =====

    [Fact]
    public async Task ReportReview_StudentValid_AndBlocksSelfAndDuplicate_AndAllowsAfterDismiss()
    {
        var reportRepo = new FakeReportRepo();
        var centerRepo = new FakeCenterRepo();
        var userRepo = new FakeUserRepo();
        var audit = new FakeAudit();
        var parentLinks = new FakeParentLinks();
        var service = new CenterReviewReportService(reportRepo, centerRepo, userRepo, audit, parentLinks);

        var author = new User { Id = Guid.NewGuid(), FullName = "Học sinh A", Role = UserRoles.Student, DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)) };
        var reporter = new User { Id = Guid.NewGuid(), FullName = "Học sinh B", Role = UserRoles.Student, DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-18)) };
        var minor = new User { Id = Guid.NewGuid(), FullName = "Học sinh nhỏ", Role = UserRoles.Student, DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14)) };
        var center = VerifiedCenter(Guid.NewGuid());
        var review = new CenterReview { StudyCenterId = center.Id, UserId = author.Id, Rating = 2, Comment = "Nội dung", Status = "visible" };

        userRepo.Users.AddRange([author, reporter, minor]);
        centerRepo.Centers.Add(center);
        reportRepo.Reviews.Add(review);

        // E1: không tự báo cáo đánh giá của mình
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReportAsync(author.Id, author.Email, author.Role, center.Id, review.Id, new CreateCenterReviewReportRequest(CenterReviewReportReasons.SpamAd)));

        // E3: học sinh <16 chưa có đồng ý phụ huynh bị chặn
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ReportAsync(minor.Id, minor.Email, minor.Role, center.Id, review.Id, new CreateCenterReviewReportRequest(CenterReviewReportReasons.SpamAd)));

        // Hợp lệ
        var created = await service.ReportAsync(reporter.Id, reporter.Email, reporter.Role, center.Id, review.Id,
            new CreateCenterReviewReportRequest(CenterReviewReportReasons.OffensiveLanguage, "Có từ xúc phạm"));
        Assert.False(created.IsCenterSelfReport);
        Assert.Equal(CenterReviewReportStatuses.Open, created.Status);
        Assert.Single(audit.Logs);

        // E2: báo cáo trùng khi báo cáo cũ đang mở bị chặn
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ReportAsync(reporter.Id, reporter.Email, reporter.Role, center.Id, review.Id, new CreateCenterReviewReportRequest(CenterReviewReportReasons.SpamAd)));

        // Moderator bác bỏ -> người dùng có thể báo cáo lại nếu cần
        await service.DismissAsync(Guid.NewGuid(), created.Id, "Không vi phạm");
        Assert.Equal(CenterReviewReportStatuses.Dismissed, reportRepo.Reports.Single().Status);

        var reReported = await service.ReportAsync(reporter.Id, reporter.Email, reporter.Role, center.Id, review.Id,
            new CreateCenterReviewReportRequest(CenterReviewReportReasons.Other, "Báo cáo lại"));
        Assert.Equal(2, reportRepo.Reports.Count);
        Assert.Equal(CenterReviewReportStatuses.Open, reReported.Status);
    }

    [Fact]
    public async Task ReportReview_CenterSelfReportFlagged_AndOtherCenterRejected()
    {
        var reportRepo = new FakeReportRepo();
        var centerRepo = new FakeCenterRepo();
        var userRepo = new FakeUserRepo();
        var audit = new FakeAudit();
        var service = new CenterReviewReportService(reportRepo, centerRepo, userRepo, audit);

        var owner1 = new User { Id = Guid.NewGuid(), FullName = "Trung tâm 1", Email = "c1@test.vn", Role = UserRoles.Center };
        var owner2 = new User { Id = Guid.NewGuid(), FullName = "Trung tâm 2", Email = "c2@test.vn", Role = UserRoles.Center };
        var center1 = VerifiedCenter(owner1.Id, "Trung tâm 1");
        var center2 = VerifiedCenter(owner2.Id, "Trung tâm 2");
        var review = new CenterReview { StudyCenterId = center1.Id, UserId = Guid.NewGuid(), Rating = 1, Comment = "Không đúng sự thật", Status = "visible" };

        userRepo.Users.AddRange([owner1, owner2]);
        centerRepo.Centers.AddRange([center1, center2]);
        reportRepo.Reviews.Add(review);

        // E4: trung tâm 2 không được báo cáo đánh giá của trung tâm 1
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ReportAsync(owner2.Id, owner2.Email, owner2.Role, center1.Id, review.Id,
                new CreateCenterReviewReportRequest(CenterReviewReportReasons.NotRealExperience)));

        // 1a: trung tâm 1 báo cáo đánh giá về chính mình -> IsCenterSelfReport = true
        var dto = await service.ReportAsync(owner1.Id, owner1.Email, owner1.Role, center1.Id, review.Id,
            new CreateCenterReviewReportRequest(CenterReviewReportReasons.NotRealExperience, "Không có hồ sơ liên hệ"));
        Assert.True(dto.IsCenterSelfReport);
    }

    // ===== UC-152: Phản hồi đánh giá trung tâm =====

    [Fact]
    public async Task ReplyReview_CreateNotifiesReviewer_BlocksDuplicateAndContactInfo_AndEditWithin24h()
    {
        var centerRepo = new FakeCenterRepo();
        var reviewRepo = new FakeModerationRepo();
        var audit = new FakeAudit();
        var notifications = new FakeNotifications();
        var service = new CenterReviewReplyService(centerRepo, reviewRepo, reviewRepo, reviewRepo, audit, notifications);

        var ownerId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var center = VerifiedCenter(ownerId, "Du Học Toàn Cầu");
        var review = new CenterReview { StudyCenterId = center.Id, UserId = reviewerId, Rating = 4, Comment = "Tư vấn nhiệt tình", Status = "visible" };
        centerRepo.Centers.Add(center);
        reviewRepo.Reviews.Add(review);

        // E2: rỗng, quá 1000 ký tự, chứa SĐT / email / link đều bị chặn
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("   ")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest(new string('a', 1001))));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Liên hệ 0912345678 nhé")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Gửi về support@center.vn")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Xem tại https://center.vn")));

        // Đăng phản hồi hợp lệ -> thông báo người đánh giá + ghi audit
        var created = await service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("  Cảm ơn bạn đã chia sẻ trải nghiệm!  "));
        Assert.NotNull(created.CenterReply);
        Assert.Equal("Cảm ơn bạn đã chia sẻ trải nghiệm!", created.CenterReply.Content);
        Assert.Null(created.CenterReply.EditedAt);
        var notif = Assert.Single(notifications.Sent);
        Assert.Equal(reviewerId, notif.UserId);
        Assert.Equal("center_review_reply", notif.Type);

        // E1: không được đăng phản hồi thứ 2
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Phản hồi thêm")));

        // Flow 2a: sửa trong 24h -> thành công và gắn EditedAt
        var edited = await service.EditReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Cảm ơn bạn rất nhiều!"));
        Assert.Equal("Cảm ơn bạn rất nhiều!", edited.CenterReply!.Content);
        Assert.NotNull(edited.CenterReply.EditedAt);

        // Quá 24h -> từ chối sửa
        review.CenterReplyCreatedAt = DateTime.UtcNow.AddHours(-25);
        await Assert.ThrowsAsync<ArgumentException>(() => service.EditReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Sửa muộn")));
    }

    [Fact]
    public async Task ReplyReview_E3_HiddenOrUnverifiedCenter_IsRejected()
    {
        var centerRepo = new FakeCenterRepo();
        var reviewRepo = new FakeModerationRepo();
        var service = new CenterReviewReplyService(centerRepo, reviewRepo, reviewRepo, reviewRepo, new FakeAudit());

        var ownerId = Guid.NewGuid();
        var center = VerifiedCenter(ownerId);
        var review = new CenterReview { StudyCenterId = center.Id, UserId = Guid.NewGuid(), Rating = 3, Comment = "Ổn", Status = "visible" };
        centerRepo.Centers.Add(center);
        reviewRepo.Reviews.Add(review);

        center.IsActive = false;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Cảm ơn")));

        center.IsActive = true;
        center.VerificationStatus = CenterVerificationStatuses.Unverified;
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateReplyAsync(ownerId, null, review.Id, new UpsertCenterReviewReplyRequest("Cảm ơn")));
    }

    // ===== UC-53 2b: Moderator gỡ phản hồi & tự đóng báo cáo khi gỡ đánh giá =====

    [Fact]
    public async Task Moderation_RemoveReply_HidesReplyKeepsReview_AndRemoveReviewResolvesReports()
    {
        var reviewRepo = new FakeModerationRepo();
        var reportRepo = new FakeReportRepo();
        var audit = new FakeAudit();
        var moderation = new CenterReviewModerationService(reviewRepo, audit, reportRepo);

        var centerId = Guid.NewGuid();
        var modId = Guid.NewGuid();
        var review = new CenterReview
        {
            StudyCenterId = centerId,
            UserId = Guid.NewGuid(),
            Rating = 2,
            Comment = "Đánh giá gốc",
            Status = "visible",
            CenterReplyContent = "Phản hồi vi phạm",
            CenterReplyAuthorUserId = Guid.NewGuid(),
            CenterReplyCreatedAt = DateTime.UtcNow,
            CenterReplyStatus = "visible",
        };
        reviewRepo.Reviews.Add(review);
        reportRepo.Reports.Add(new CenterReviewReport
        {
            CenterReviewId = review.Id,
            StudyCenterId = centerId,
            ReporterUserId = Guid.NewGuid(),
            ReporterRole = UserRoles.Student,
            Reason = CenterReviewReportReasons.OffensiveLanguage,
            Status = CenterReviewReportStatuses.Open,
        });

        // Gỡ phản hồi: đánh giá gốc vẫn visible, phản hồi bị removed
        await moderation.RemoveReplyAsync(modId, centerId, review.Id, "Chứa thông tin cá nhân học sinh");
        Assert.Equal("visible", review.Status);
        Assert.Equal("removed", review.CenterReplyStatus);
        Assert.Equal(modId, review.CenterReplyRemovedByUserId);
        Assert.Null((await reviewRepo.GetPublicDtoAsync(review.Id))!.CenterReply);

        // Gỡ đánh giá: tự động đóng các báo cáo đang mở sang resolved_removed
        await moderation.RemoveAsync(modId, centerId, review.Id, "Nội dung vi phạm");
        Assert.Equal("removed", review.Status);
        Assert.Equal(1, reportRepo.ResolvedByRemovalCount);
        Assert.Equal(CenterReviewReportStatuses.ResolvedRemoved, reportRepo.Reports.Single().Status);
    }
}
