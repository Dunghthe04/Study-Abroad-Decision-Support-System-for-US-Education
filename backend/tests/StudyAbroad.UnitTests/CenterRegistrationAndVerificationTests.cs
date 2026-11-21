using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System.Text.RegularExpressions;
using Xunit;

namespace StudyAbroad.UnitTests;

public class CenterRegistrationAndVerificationTests
{
    private sealed class FakeStudyCenterRepo : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
        {
            var q = Centers.AsEnumerable();
            if (query.VerifiedOnly == true)
                q = q.Where(c => c.VerificationStatus == CenterReferral.Verified);
            else if (!string.IsNullOrWhiteSpace(query.VerificationStatus))
                q = q.Where(c => c.VerificationStatus == query.VerificationStatus);

            var list = q.ToList();
            return Task.FromResult(new PagedResult<StudyCenter>(list, query.Page, query.PageSize, list.Count));
        }

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(Centers.Where(c => c.IsActive && c.VerificationStatus == CenterReferral.Verified).ToList());

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Centers.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(StudyCenter center, CancellationToken ct = default)
        {
            if (center.Id == Guid.Empty) center.Id = Guid.NewGuid();
            Centers.Add(center);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default)
        {
            var center = Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId);
            if (center is not null) return Task.FromResult<StudyCenter?>(center);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var norm = email.Trim().ToLowerInvariant();
                center = Centers.FirstOrDefault(c => c.Email != null && c.Email.ToLowerInvariant() == norm);
            }
            return Task.FromResult(center);
        }
    }

    private sealed class FakeVerificationRepo : ICenterVerificationRepository
    {
        public List<CenterVerificationRequest> Requests { get; } = [];
        public List<CenterVerificationDocument> Documents { get; } = [];

        public Task<CenterVerificationRequest?> GetCurrentByCenterIdAsync(Guid centerId, CancellationToken ct = default) =>
            Task.FromResult(Requests.Where(r => r.CenterId == centerId).OrderByDescending(r => r.SubmittedAt).FirstOrDefault());

        public Task<CenterVerificationRequest?> GetByIdWithDocumentsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

        public Task<PagedResult<CenterVerificationRequest>> SearchAsync(CenterVerificationQuery query, CancellationToken ct = default)
        {
            var q = Requests.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(query.Status))
                q = q.Where(r => r.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase));
            var list = q.ToList();
            return Task.FromResult(new PagedResult<CenterVerificationRequest>(list, query.Page, query.PageSize, list.Count));
        }

        public Task AddRequestAsync(CenterVerificationRequest request, CancellationToken ct = default)
        {
            if (request.Id == Guid.Empty) request.Id = Guid.NewGuid();
            Requests.Add(request);
            foreach (var doc in request.Documents)
            {
                if (doc.Id == Guid.Empty) doc.Id = Guid.NewGuid();
                doc.RequestId = request.Id;
                Documents.Add(doc);
            }
            return Task.CompletedTask;
        }

        public Task UpdateRequestAsync(CenterVerificationRequest request, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<CenterVerificationDocument?> GetDocumentByIdAsync(Guid documentId, CancellationToken ct = default) =>
            Task.FromResult(Documents.FirstOrDefault(d => d.Id == documentId));

        public Task<bool> ExistsVerifiedTaxCodeAsync(string taxCode, Guid? excludeCenterId = null, CancellationToken ct = default)
        {
            return Task.FromResult(false);
        }
    }

    private sealed class FakeAuditLogs : IAuditLogRepository
    {
        public List<AuditLog> Logs { get; } = [];
        public Task AddAsync(AuditLog entry, CancellationToken cancellationToken = default)
        {
            Logs.Add(entry);
            return Task.CompletedTask;
        }
        public Task<string?> GetLatestLockReasonAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    [Fact]
    public async Task SubmitVerification_FailsWhenEmailNotVerified()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var verifRepo = new FakeVerificationRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit, verifRepo);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm Du học ABC",
            OwnerUserId = Guid.NewGuid(),
            EmailVerifiedAt = null // Chưa xác thực email
        };
        await centerRepo.AddAsync(center);

        var req = new SubmitVerificationDto(
            LegalName: "Công ty TNHH Tư vấn Du học ABC",
            TaxCode: "0101234567",
            Address: "Hà Nội",
            LegalRepresentative: "Nguyễn Văn A",
            Documents: [new("business_registration", "/uploads/biz.pdf", "biz.pdf", 1000, "application/pdf")]
        );

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, req);
        Assert.False(result.Success);
        Assert.Contains("xác thực email", result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitVerification_FailsWhenMissingBusinessRegistrationDocument()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var verifRepo = new FakeVerificationRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit, verifRepo);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm Du học ABC",
            OwnerUserId = Guid.NewGuid(),
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow
        };
        await centerRepo.AddAsync(center);

        var req = new SubmitVerificationDto(
            LegalName: "Công ty TNHH Tư vấn Du học ABC",
            TaxCode: "0101234567",
            Address: "Hà Nội",
            LegalRepresentative: "Nguyễn Văn A",
            Documents: [] // Thiếu GCN ĐKDN
        );

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, req);
        Assert.False(result.Success);
        Assert.Contains("Giấy chứng nhận đăng ký doanh nghiệp", result.ErrorMessage);
    }

    [Fact]
    public async Task SubmitVerification_SucceedsWhenValid_AndSetsStatusToPendingReview()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var verifRepo = new FakeVerificationRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit, verifRepo);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm Du học ABC",
            OwnerUserId = Guid.NewGuid(),
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow,
            VerificationStatus = CenterVerificationStatuses.Unverified
        };
        await centerRepo.AddAsync(center);

        var req = new SubmitVerificationDto(
            LegalName: "Công ty TNHH Tư vấn Du học ABC",
            TaxCode: "0101234567",
            Address: "Hà Nội",
            LegalRepresentative: "Nguyễn Văn A",
            Documents: [new("business_registration", "/uploads/biz.pdf", "biz.pdf", 2048, "application/pdf")]
        );

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, req);
        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.PendingReview, center.VerificationStatus);
        Assert.Equal("0101234567", center.TaxCode);
        Assert.Single(verifRepo.Requests);
        Assert.Equal(CenterVerificationStatuses.PendingReview, verifRepo.Requests[0].Status);
        Assert.Contains(audit.Logs, l => l.Action == "center.verification_submitted");
    }

    [Fact]
    public async Task ReviewVerification_ApproveRequiresAll5Conditions_BR_C3()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var verifRepo = new FakeVerificationRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit, verifRepo);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm Du học ABC",
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow,
            VerificationStatus = CenterVerificationStatuses.PendingReview
        };
        await centerRepo.AddAsync(center);

        var req = new CenterVerificationRequest
        {
            Id = Guid.NewGuid(),
            CenterId = center.Id,
            LegalName = "Công ty TNHH Tư vấn Du học ABC",
            TaxCode = "0101234567",
            Status = CenterVerificationStatuses.PendingReview
        };
        await verifRepo.AddRequestAsync(req);

        // Moderator chưa tick điều kiện 2 (Cổng ĐKDN)
        var reviewReqIncomplete = new ReviewCenterVerificationRequest(
            Action: "approve",
            IsNationalRegistryMatched: false,
            IsBusinessOperating: true,
            IsRepresentativeValid: true
        );
        var resultIncomplete = await service.ReviewVerificationAsync(req.Id, reviewReqIncomplete, Guid.NewGuid());
        Assert.False(resultIncomplete.Success);
        Assert.Contains("Điều kiện (2)", resultIncomplete.ErrorMessage);

        // Moderator tick đủ cả 3 điều kiện
        var reviewReqComplete = new ReviewCenterVerificationRequest(
            Action: "approve",
            IsNationalRegistryMatched: true,
            IsBusinessOperating: true,
            IsRepresentativeValid: true,
            ReviewNotes: "Hồ sơ đối chiếu hoàn toàn trùng khớp"
        );
        var resultComplete = await service.ReviewVerificationAsync(req.Id, reviewReqComplete, Guid.NewGuid());
        Assert.True(resultComplete.Success);
        Assert.Equal(CenterVerificationStatuses.Verified, center.VerificationStatus);
        Assert.NotNull(center.VerifiedAt);
        Assert.Contains(audit.Logs, l => l.Action == "center.verified");
    }

    [Fact]
    public async Task ReviewVerification_RejectRequiresReason()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var verifRepo = new FakeVerificationRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit, verifRepo);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            VerificationStatus = CenterVerificationStatuses.PendingReview
        };
        await centerRepo.AddAsync(center);

        var req = new CenterVerificationRequest
        {
            Id = Guid.NewGuid(),
            CenterId = center.Id,
            Status = CenterVerificationStatuses.PendingReview
        };
        await verifRepo.AddRequestAsync(req);

        // Từ chối không có lý do
        var rejectEmpty = new ReviewCenterVerificationRequest(Action: "reject", RejectionReason: "");
        var res1 = await service.ReviewVerificationAsync(req.Id, rejectEmpty, Guid.NewGuid());
        Assert.False(res1.Success);
        Assert.Contains("Lý do từ chối", res1.ErrorMessage);

        // Từ chối có lý do cụ thể
        var rejectValid = new ReviewCenterVerificationRequest(Action: "reject", RejectionReason: "Ảnh bản scan ĐKKD bị mờ, không đọc được MST.");
        var res2 = await service.ReviewVerificationAsync(req.Id, rejectValid, Guid.NewGuid());
        Assert.True(res2.Success);
        Assert.Equal(CenterVerificationStatuses.Rejected, center.VerificationStatus);
        Assert.Equal("Ảnh bản scan ĐKKD bị mờ, không đọc được MST.", center.VerificationNote);
        Assert.Contains(audit.Logs, l => l.Action == "center.reject");
    }

    [Fact]
    public async Task UpdateCenterProfile_FeeMinCannotExceedFeeMax_BR_C9()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var audit = new FakeAuditLogs();
        var service = new StudyCenterService(centerRepo, audit);

        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid()
        };
        await centerRepo.AddAsync(center);

        var invalidFeeReq = new UpdateCenterProfileRequest(
            FeeMin: 50000000m,
            FeeMax: 20000000m // Max < Min
        );

        var result = await service.UpdateCenterProfileAsync(center.OwnerUserId!.Value, invalidFeeReq);
        Assert.False(result.Success);
        Assert.Contains("không được lớn hơn", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateCenterProfile_NotVerified_CanEditLegalFields()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var service = new StudyCenterService(centerRepo, new FakeAuditLogs());
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid(),
            VerificationStatus = CenterVerificationStatuses.Unverified,
        };
        await centerRepo.AddAsync(center);

        var result = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value,
            new UpdateCenterProfileRequest(Name: "Trung tâm ABC Mới", TaxCode: "0312345678", LegalRepresentative: "Nguyễn Văn A"));

        Assert.True(result.Success);
        Assert.Equal("0312345678", center.TaxCode);
        Assert.Equal("Nguyễn Văn A", center.LegalRepresentative);
        Assert.Equal("Trung tâm ABC Mới", center.Name);
    }

    [Fact]
    public async Task UpdateCenterProfile_Verified_LegalChangeGoesBackToPendingReview_MarketingFieldsSaveDirectly()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var service = new StudyCenterService(centerRepo, new FakeAuditLogs());
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid(),
            VerificationStatus = CenterVerificationStatuses.Verified,
            TaxCode = "0312345678",
        };
        await centerRepo.AddAsync(center);

        var marketing = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value,
            new UpdateCenterProfileRequest(Description: "Mô tả mới", FeeMin: 1000000m, FeeMax: 5000000m));
        Assert.True(marketing.Success);
        Assert.Equal("Mô tả mới", center.Description);
        Assert.Equal(CenterVerificationStatuses.Verified, center.VerificationStatus);

        // [UC-92] Đã xác minh mà đổi thông tin pháp lý: lưu và đưa hồ sơ về Chờ duyệt (tạm ẩn dấu xác minh)
        var legalChange = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value, new UpdateCenterProfileRequest(TaxCode: "0999999999"));
        Assert.True(legalChange.Success);
        Assert.Equal("0999999999", center.TaxCode);
        Assert.Equal(CenterVerificationStatuses.PendingReview, center.VerificationStatus);
        Assert.True(legalChange.Outcome!.VerificationReset);
    }

    // ===== [UC-59] Email kết quả xét duyệt cho trung tâm, thứ tự hàng đợi =====

    private static (StudyCenterService Service, FakeEmailSender Email, CenterVerificationRequest Req, StudyCenter Center) NotificationSetup(
        string? centerEmail = "trungtam@example.com", bool emailVerified = true)
    {
        var centers = new FakeStudyCenterRepo();
        var verifications = new FakeVerificationRepo();
        var email = new FakeEmailSender();
        var service = new StudyCenterService(centers, new FakeAuditLogs(), verifications, email);
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            Email = centerEmail,
            EmailVerifiedAt = emailVerified ? DateTime.UtcNow : null,
            VerificationStatus = CenterVerificationStatuses.PendingReview,
        };
        centers.AddAsync(center).GetAwaiter().GetResult();
        var req = new CenterVerificationRequest { Id = Guid.NewGuid(), CenterId = center.Id, Status = CenterVerificationStatuses.PendingReview };
        verifications.AddRequestAsync(req).GetAwaiter().GetResult();
        return (service, email, req, center);
    }

    [Fact]
    public async Task Review_Reject_EmailsTheCenterTheReasonAndTheUnmetConditions()
    {
        var (service, email, req, _) = NotificationSetup();

        var result = await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(
            Action: "reject", IsNationalRegistryMatched: false, IsBusinessOperating: true, IsRepresentativeValid: false,
            RejectionReason: "  Ảnh ĐKKD bị mờ  "), Guid.NewGuid());

        Assert.True(result.Success);
        var sent = Assert.Single(email.CenterVerificationEmails);
        Assert.Equal("trungtam@example.com", sent.To);
        Assert.Equal("Trung tâm ABC", sent.CenterName);
        Assert.Equal("reject", sent.Outcome);
        Assert.Equal("Ảnh ĐKKD bị mờ", sent.Reason);
        Assert.Equal(2, sent.Unmet!.Count);
        Assert.Contains(sent.Unmet, c => c.Contains("mã số thuế"));
        Assert.Contains(sent.Unmet, c => c.Contains("đại diện theo pháp luật"));
        Assert.DoesNotContain(sent.Unmet, c => c.Contains("hoạt động"));
    }

    [Fact]
    public async Task Review_ChangesRequested_ListsAnUnverifiedEmailAsUnmet()
    {
        var (service, email, req, _) = NotificationSetup(emailVerified: false);

        await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(
            Action: "changes_requested", IsNationalRegistryMatched: true, IsBusinessOperating: true, IsRepresentativeValid: true,
            RejectionReason: "Bổ sung giấy ủy quyền"), Guid.NewGuid());

        var sent = Assert.Single(email.CenterVerificationEmails);
        Assert.Equal("changes_requested", sent.Outcome);
        Assert.Contains("Email trung tâm", Assert.Single(sent.Unmet!));
    }

    [Fact]
    public async Task Review_Approve_EmailsVerifiedWithoutUnmetConditions()
    {
        var (service, email, req, _) = NotificationSetup();

        await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(
            Action: "approve", IsNationalRegistryMatched: true, IsBusinessOperating: true, IsRepresentativeValid: true,
            ReviewNotes: "Khớp hoàn toàn"), Guid.NewGuid());

        var sent = Assert.Single(email.CenterVerificationEmails);
        Assert.Equal("verified", sent.Outcome);
        Assert.Null(sent.Unmet);
    }

    [Fact]
    public async Task Review_ApproveBlockedByRules_SendsNoEmail()
    {
        var (service, email, req, center) = NotificationSetup();

        var result = await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(
            Action: "approve", IsNationalRegistryMatched: false, IsBusinessOperating: true, IsRepresentativeValid: true), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Empty(email.CenterVerificationEmails);
        Assert.Equal(CenterVerificationStatuses.PendingReview, center.VerificationStatus);
    }

    [Fact]
    public async Task Review_CenterWithoutEmail_StillSavesTheDecisionAndSendsNothing()
    {
        var (service, email, req, center) = NotificationSetup(centerEmail: null);

        var result = await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(Action: "reject", RejectionReason: "Thiếu hồ sơ"), Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.Rejected, center.VerificationStatus);
        Assert.Empty(email.CenterVerificationEmails);
    }

    [Fact]
    public async Task Review_WhenTheEmailSenderFails_TheDecisionIsStillSaved()
    {
        var (service, email, req, center) = NotificationSetup();
        email.ThrowOnCenterVerificationEmail = true;

        var result = await service.ReviewVerificationAsync(req.Id, new ReviewCenterVerificationRequest(Action: "reject", RejectionReason: "Thiếu hồ sơ"), Guid.NewGuid());

        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.Rejected, center.VerificationStatus);
    }

    [Fact]
    public void VerificationQueue_PendingFirstOldestFirst_ThenProcessedNewestFirst()
    {
        var now = DateTime.UtcNow;
        CenterVerificationRequest R(string name, string status, int daysAgo) =>
            new() { Id = Guid.NewGuid(), LegalName = name, Status = status, SubmittedAt = now.AddDays(-daysAgo) };
        var requests = new[]
        {
            R("pending-new", CenterVerificationStatuses.PendingReview, 1),
            R("verified-newest", CenterVerificationStatuses.Verified, 0),
            R("pending-old", CenterVerificationStatuses.PendingReview, 5),
            R("rejected-older", CenterVerificationStatuses.Rejected, 3),
            R("pending-mid", CenterVerificationStatuses.PendingReview, 3),
        };

        var ordered = StudyAbroad.Infrastructure.Persistence.Repositories.CenterVerificationRepository
            .OrderQueue(requests.AsQueryable()).Select(r => r.LegalName).ToList();

        Assert.Equal(["pending-old", "pending-mid", "pending-new", "verified-newest", "rejected-older"], ordered);
    }

    [Fact]
    public void VerificationQueue_OrderingTranslatesToSql()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<StudyAbroad.Infrastructure.Persistence.AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new StudyAbroad.Infrastructure.Persistence.AppDbContext(options);

        var sql = StudyAbroad.Infrastructure.Persistence.Repositories.CenterVerificationRepository
            .OrderQueue(db.CenterVerificationRequests).ToQueryString();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("CASE", sql);
    }

    // ===== [UC-71] Nộp hồ sơ xác minh: giới hạn tệp, email xác nhận, khóa sửa khi chờ duyệt =====

    private static (StudyCenterService Service, FakeEmailSender Email, FakeVerificationRepo Verifications, StudyCenter Center) SubmitSetup(string? centerEmail = "trungtam@example.com")
    {
        var centers = new FakeStudyCenterRepo();
        var verifications = new FakeVerificationRepo();
        var email = new FakeEmailSender();
        var service = new StudyCenterService(centers, new FakeAuditLogs(), verifications, email);
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            Email = centerEmail,
            OwnerUserId = Guid.NewGuid(),
            EmailVerifiedAt = DateTime.UtcNow,
            PhoneVerifiedAt = DateTime.UtcNow,
            VerificationStatus = CenterVerificationStatuses.Unverified,
        };
        centers.AddAsync(center).GetAwaiter().GetResult();
        return (service, email, verifications, center);
    }

    private static SubmitVerificationDto SubmitRequest(int documentCount) => new(
        LegalName: "Công ty TNHH Tư vấn Du học ABC",
        TaxCode: "0101234567",
        Address: "Hà Nội",
        LegalRepresentative: "Nguyễn Văn A",
        Documents: Enumerable.Range(0, documentCount)
            .Select(i => new VerificationDocumentUploadDto(i == 0 ? "business_registration" : "power_of_attorney", $"/uploads/f{i}.pdf", $"f{i}.pdf", 1000, "application/pdf"))
            .ToList());

    [Fact]
    public async Task Submit_FiveDocuments_IsAcceptedAndSixIsRejected()
    {
        var (service, _, verifications, center) = SubmitSetup();

        var tooMany = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(StudyCenterService.MaxVerificationDocuments + 1));
        Assert.False(tooMany.Success);
        Assert.Contains("tối đa 5 tệp", tooMany.ErrorMessage);
        Assert.Equal(CenterVerificationStatuses.Unverified, center.VerificationStatus);

        var ok = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(StudyCenterService.MaxVerificationDocuments));
        Assert.True(ok.Success);
        Assert.Equal(CenterVerificationStatuses.PendingReview, center.VerificationStatus);
    }

    [Fact]
    public async Task Submit_Success_EmailsAnAcknowledgementToTheCenter()
    {
        var (service, email, _, center) = SubmitSetup();

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(1));

        Assert.True(result.Success);
        var sent = Assert.Single(email.CenterVerificationEmails);
        Assert.Equal("trungtam@example.com", sent.To);
        Assert.Equal("submitted", sent.Outcome);
        Assert.Null(sent.Reason);
    }

    [Fact]
    public async Task Submit_RejectedByRules_SendsNoEmail()
    {
        var (service, email, _, center) = SubmitSetup();

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(0));

        Assert.False(result.Success);
        Assert.Empty(email.CenterVerificationEmails);
    }

    [Fact]
    public async Task UpdateCenterProfile_WhilePendingReview_LocksLegalFieldsButAllowsMarketingFields()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var service = new StudyCenterService(centerRepo, new FakeAuditLogs());
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid(),
            VerificationStatus = CenterVerificationStatuses.PendingReview,
            TaxCode = "0312345678",
        };
        await centerRepo.AddAsync(center);

        var legal = await service.UpdateCenterProfileAsync(center.OwnerUserId!.Value, new UpdateCenterProfileRequest(TaxCode: "0999999999"));
        Assert.False(legal.Success);
        Assert.Contains("chờ duyệt", legal.ErrorMessage);
        Assert.Equal("0312345678", center.TaxCode);

        var rename = await service.UpdateCenterProfileAsync(center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Name: "Tên khác"));
        Assert.False(rename.Success);
        Assert.Equal("Trung tâm ABC", center.Name);

        var marketing = await service.UpdateCenterProfileAsync(center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Description: "Mô tả mới"));
        Assert.True(marketing.Success);
        Assert.Equal("Mô tả mới", center.Description);
    }

    [Fact]
    public async Task UpdateCenterProfile_Unverified_StillAllowsLegalFieldChanges()
    {
        var centerRepo = new FakeStudyCenterRepo();
        var service = new StudyCenterService(centerRepo, new FakeAuditLogs());
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid(),
            VerificationStatus = CenterVerificationStatuses.Unverified,
        };
        await centerRepo.AddAsync(center);

        var result = await service.UpdateCenterProfileAsync(center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Name: "Tên pháp lý mới"));

        Assert.True(result.Success);
        Assert.Equal("Tên pháp lý mới", center.Name);
    }

    // ===== [UC-71] Giấy ủy quyền bắt buộc khi người đăng ký khác người đại diện pháp luật =====

    [Theory]
    [InlineData("Nguyễn Văn A", "Nguyễn Văn A", false)]
    [InlineData("  nguyen van a ", "Nguyễn Văn A", false)]
    [InlineData("Đỗ Văn B", "do  van b", false)]
    [InlineData("Trần Thị C", "Nguyễn Văn A", true)]
    [InlineData(null, "Nguyễn Văn A", false)]
    [InlineData("", "Nguyễn Văn A", false)]
    public void RegistrantNeedsPowerOfAttorney_ComparesNamesIgnoringCaseAccentsAndSpaces(string? registrant, string rep, bool expected)
    {
        Assert.Equal(expected, StudyCenterService.RegistrantNeedsPowerOfAttorney(registrant, rep));
    }

    [Fact]
    public async Task Submit_RegistrantIsNotTheLegalRepresentative_RequiresPowerOfAttorney()
    {
        var (service, email, _, center) = SubmitSetup();
        center.RegistrantName = "Trần Thị C";

        var without = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, new SubmitVerificationDto(
            "Công ty ABC", "0101234567", "Hà Nội", "Nguyễn Văn A",
            Documents: [new VerificationDocumentUploadDto("business_registration", "/uploads/a.pdf", "a.pdf", 1000, "application/pdf")]));
        Assert.False(without.Success);
        Assert.Contains("Giấy ủy quyền", without.ErrorMessage);
        Assert.Equal(CenterVerificationStatuses.Unverified, center.VerificationStatus);
        Assert.Empty(email.CenterVerificationEmails);

        var with = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(2));
        Assert.True(with.Success);
    }

    [Fact]
    public async Task Submit_RegistrantIsTheLegalRepresentative_DoesNotNeedPowerOfAttorney()
    {
        var (service, _, _, center) = SubmitSetup();
        center.RegistrantName = "nguyen van a";

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, new SubmitVerificationDto(
            "Công ty ABC", "0101234567", "Hà Nội", "Nguyễn Văn A",
            Documents: [new VerificationDocumentUploadDto("business_registration", "/uploads/a.pdf", "a.pdf", 1000, "application/pdf")]));

        Assert.True(result.Success);
    }

    [Fact]
    public async Task Submit_PhoneNeverVerified_IsNotABlocker()
    {
        // [UC-91] Số điện thoại không xác thực bằng SMS OTP nữa: chỉ email là điều kiện nộp hồ sơ
        var (service, _, _, center) = SubmitSetup();
        center.PhoneVerifiedAt = null;

        var result = await service.SubmitVerificationAsync(center.OwnerUserId!.Value, SubmitRequest(1));

        Assert.True(result.Success);
    }
    // ===== [UC-92] Phân loại thay đổi khi trung tâm tự sửa hồ sơ, yêu cầu xóa tài khoản =====

    private sealed class FakeProfileChangeRepo : ICenterProfileChangeRepository
    {
        public List<CenterProfileChange> Items { get; } = [];

        public Task<CenterProfileChange?> GetPendingAsync(Guid studyCenterId, string field, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.StudyCenterId == studyCenterId && x.Field == field && x.Status == CenterProfileChangeStatuses.Pending));

        public Task<CenterProfileChange?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(CenterProfileChange change, CancellationToken ct = default)
        {
            Items.Add(change);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CenterProfileChange change, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<CenterProfileChange>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<CenterProfileChange>(
                Items.Where(x => status == null || x.Status == status).ToList(), page, pageSize, Items.Count));

        public Task<IReadOnlyList<CenterChangeCenterInfo>> GetCenterInfosAsync(IReadOnlyCollection<Guid> centerIds, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CenterChangeCenterInfo>>([]);

        public Task AddNotificationAsync(Notification notification, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDeletionRepo : ICenterDeletionRequestRepository
    {
        public List<CenterDeletionRequest> Items { get; } = [];

        public Task<CenterDeletionRequest?> GetOpenByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x =>
                x.StudyCenterId == studyCenterId && x.Status == CenterDeletionRequestStatuses.Pending));

        public Task<CenterDeletionRequest?> GetLatestByCenterIdAsync(Guid studyCenterId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(x => x.StudyCenterId == studyCenterId).OrderByDescending(x => x.CreatedAt).FirstOrDefault());

        public Task<CenterDeletionRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(x => x.Id == id));

        public Task AddAsync(CenterDeletionRequest request, CancellationToken ct = default)
        {
            Items.Add(request);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(CenterDeletionRequest request, CancellationToken ct = default) => Task.CompletedTask;

        public Task<PagedResult<CenterDeletionRequest>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(new PagedResult<CenterDeletionRequest>(Items, page, pageSize, Items.Count));
    }

    private sealed record ProfileSetup(
        StudyCenterService Service,
        StudyCenter Center,
        FakeAuditLogs Audit,
        FakeVerificationRepo Verifications,
        FakeProfileChangeRepo Changes,
        FakeDeletionRepo Deletions);

    private static async Task<ProfileSetup> ProfileEditSetup(string status, Action<StudyCenter>? configure = null)
    {
        var centers = new FakeStudyCenterRepo();
        var audit = new FakeAuditLogs();
        var verifications = new FakeVerificationRepo();
        var changes = new FakeProfileChangeRepo();
        var deletions = new FakeDeletionRepo();
        var center = new StudyCenter
        {
            Id = Guid.NewGuid(),
            Name = "Trung tâm ABC",
            OwnerUserId = Guid.NewGuid(),
            VerificationStatus = status,
            TaxCode = "0312345678",
            LegalRepresentative = "Nguyễn Văn A",
            Address = "1 Lê Lợi, Quận 1",
            Website = "https://abc.edu.vn",
        };
        configure?.Invoke(center);
        await centers.AddAsync(center);
        var service = new StudyCenterService(centers, audit, verifications, emailSender: null, notifications: null, profileChanges: changes, deletionRequests: deletions);
        return new ProfileSetup(service, center, audit, verifications, changes, deletions);
    }

    [Fact]
    public async Task UpdateCenterProfile_DescriptionServicesLogo_SavedImmediatelyWithoutReviewOrReset()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);

        var result = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value, new UpdateCenterProfileRequest(
            Description: "Mô tả mới",
            Services: ["CHON_TRUONG", "VISA"],
            LogoUrl: "https://res.cloudinary.com/x/logo.png",
            ServiceAreas: ["Hà Nội", " Online ", "Hà Nội"],
            Branches: ["12 Trần Phú, Hà Đông"]));

        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.Verified, s.Center.VerificationStatus);
        Assert.Equal(["Hà Nội", "Online"], s.Center.ServiceAreas);
        Assert.Equal(["12 Trần Phú, Hà Đông"], s.Center.Branches);
        var outcome = Assert.IsType<CenterProfileUpdateOutcome>(result.Outcome);
        Assert.Contains("description", outcome.SavedFields);
        Assert.Contains("services", outcome.SavedFields);
        Assert.Contains("logoUrl", outcome.SavedFields);
        Assert.Contains("serviceAreas", outcome.SavedFields);
        Assert.Empty(outcome.ReviewFields);
        Assert.Empty(outcome.LegalFields);
        Assert.False(outcome.VerificationReset);
        Assert.Empty(s.Changes.Items);
        Assert.Empty(s.Verifications.Requests);
        var log = Assert.Single(s.Audit.Logs);
        Assert.Equal("center.profile_updated", log.Action);
    }

    [Fact]
    public async Task UpdateCenterProfile_WebsiteAndFanpage_SavedButFlaggedForModerator()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);

        var result = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value, new UpdateCenterProfileRequest(
            Website: "https://abc-moi.edu.vn", Fanpage: "https://facebook.com/abc"));

        Assert.True(result.Success);
        Assert.Equal("https://abc-moi.edu.vn", s.Center.Website);
        Assert.Equal("https://facebook.com/abc", s.Center.Fanpage);
        Assert.Equal(CenterVerificationStatuses.Verified, s.Center.VerificationStatus);
        Assert.Equal([CenterProfileChangeFields.Website, CenterProfileChangeFields.Fanpage], result.Outcome!.ReviewFields);

        var website = Assert.Single(s.Changes.Items, x => x.Field == CenterProfileChangeFields.Website);
        Assert.Equal("https://abc.edu.vn", website.OldValue);
        Assert.Equal("https://abc-moi.edu.vn", website.NewValue);
        Assert.Equal(CenterProfileChangeStatuses.Pending, website.Status);
        Assert.Single(s.Changes.Items, x => x.Field == CenterProfileChangeFields.Fanpage);
    }

    [Fact]
    public async Task UpdateCenterProfile_WebsiteChangedTwice_KeepsOnePendingWithOriginalOldValue_AndRevertClosesIt()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);
        var owner = s.Center.OwnerUserId!.Value;

        await s.Service.UpdateCenterProfileAsync(owner, new UpdateCenterProfileRequest(Website: "https://lan-1.edu.vn"));
        await s.Service.UpdateCenterProfileAsync(owner, new UpdateCenterProfileRequest(Website: "https://lan-2.edu.vn"));

        var pending = Assert.Single(s.Changes.Items);
        Assert.Equal("https://abc.edu.vn", pending.OldValue);
        Assert.Equal("https://lan-2.edu.vn", pending.NewValue);

        await s.Service.UpdateCenterProfileAsync(owner, new UpdateCenterProfileRequest(Website: "https://abc.edu.vn"));
        // Không phải Moderator duyệt nên dùng trạng thái riêng, không lẫn vào danh sách Đạt
        Assert.Equal(CenterProfileChangeStatuses.Withdrawn, pending.Status);
    }

    [Theory]
    [InlineData("abc.edu.vn")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://abc.edu.vn")]
    public async Task UpdateCenterProfile_InvalidWebsiteOrFanpage_IsRejected(string url)
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);

        var website = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Website: url));
        var fanpage = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Fanpage: url));

        Assert.False(website.Success);
        Assert.False(fanpage.Success);
        Assert.Equal("https://abc.edu.vn", s.Center.Website);
        Assert.Empty(s.Changes.Items);
    }

    [Fact]
    public async Task UpdateCenterProfile_VerifiedLegalChange_ResetsToPendingReviewAndCreatesReverificationRequest()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);
        var previous = new CenterVerificationRequest
        {
            CenterId = s.Center.Id, LegalName = "Trung tâm ABC", TaxCode = "0312345678",
            Address = "1 Lê Lợi", LegalRepresentative = "Nguyễn Văn A", Status = "approved",
            SubmittedAt = DateTime.UtcNow.AddDays(-30),
        };
        previous.Documents.Add(new CenterVerificationDocument
        {
            RequestId = previous.Id, CenterId = s.Center.Id, DocumentType = CenterDocumentTypes.BusinessRegistration,
            FileUrl = "uploads/dkkd.pdf", FileName = "dkkd.pdf",
        });
        await s.Verifications.AddRequestAsync(previous);

        var result = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value,
            new UpdateCenterProfileRequest(Name: "Công ty ABC Mới", TaxCode: "0999999999", Description: "Mô tả"));

        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.PendingReview, s.Center.VerificationStatus);
        Assert.Equal("0999999999", s.Center.TaxCode);
        Assert.Equal("Công ty ABC Mới", s.Center.Name);
        Assert.True(result.Outcome!.VerificationReset);
        Assert.Contains("name", result.Outcome.LegalFields);
        Assert.Contains("taxCode", result.Outcome.LegalFields);
        Assert.Contains("description", result.Outcome.SavedFields);

        var created = Assert.Single(s.Verifications.Requests, r => r.Id != previous.Id);
        Assert.Equal(CenterVerificationStatuses.PendingReview, created.Status);
        Assert.Equal("0999999999", created.TaxCode);
        Assert.Equal("Công ty ABC Mới", created.LegalName);
        Assert.Equal("uploads/dkkd.pdf", Assert.Single(created.Documents).FileUrl);
        Assert.Contains(s.Audit.Logs, l => l.Action == "center.verification_reset");
    }

    [Fact]
    public async Task UpdateCenterProfile_VerifiedSameLegalValues_DoesNotReset()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);

        var result = await s.Service.UpdateCenterProfileAsync(s.Center.OwnerUserId!.Value,
            new UpdateCenterProfileRequest(Name: "Trung tâm ABC", TaxCode: "0312345678", Description: "Mô tả"));

        Assert.True(result.Success);
        Assert.Equal(CenterVerificationStatuses.Verified, s.Center.VerificationStatus);
        Assert.False(result.Outcome!.VerificationReset);
        Assert.Empty(s.Verifications.Requests);
    }

    [Fact]
    public async Task RequestDeletion_CreatesOnePendingRequestAndLogsAudit_RejectsDuplicateAndEmptyReason()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Verified);
        var owner = s.Center.OwnerUserId!.Value;

        var empty = await s.Service.RequestDeletionAsync(owner, "   ");
        Assert.False(empty.Success);

        var first = await s.Service.RequestDeletionAsync(owner, "  Ngừng hoạt động  ");
        Assert.True(first.Success);
        Assert.Equal("Ngừng hoạt động", first.Request!.Reason);
        Assert.Equal(CenterDeletionRequestStatuses.Pending, first.Request.Status);
        Assert.Contains(s.Audit.Logs, l => l.Action == "center.deletion_requested");

        var duplicate = await s.Service.RequestDeletionAsync(owner, "Gửi lại");
        Assert.False(duplicate.Success);
        Assert.Single(s.Deletions.Items);

        var latest = await s.Service.GetDeletionRequestAsync(owner);
        Assert.Equal(first.Request.Id, latest!.Id);

        // Yêu cầu cũ đã được xử lý (UC-93) thì gửi được yêu cầu mới
        s.Deletions.Items[0].Status = CenterDeletionRequestStatuses.Rejected;
        Assert.True((await s.Service.RequestDeletionAsync(owner, "Gửi lại")).Success);
    }

    [Fact]
    public async Task RequestDeletion_UnknownCenterOrTooLongReason_IsRejected()
    {
        var s = await ProfileEditSetup(CenterVerificationStatuses.Unverified);

        Assert.False((await s.Service.RequestDeletionAsync(Guid.NewGuid(), "Lý do")).Success);
        Assert.False((await s.Service.RequestDeletionAsync(
            s.Center.OwnerUserId!.Value, new string('a', StudyCenterService.MaxDeletionReasonLength + 1))).Success);
        Assert.Empty(s.Deletions.Items);
    }
}
