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
    public async Task SubmitVerification_FailsWhenPhoneNotVerified()
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
    public async Task UpdateCenterProfile_Verified_RejectsLegalChange_ButAllowsMarketingFields()
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

        var legalChange = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value, new UpdateCenterProfileRequest(TaxCode: "0999999999"));
        Assert.False(legalChange.Success);
        Assert.Equal("0312345678", center.TaxCode);

        var renameAttempt = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value, new UpdateCenterProfileRequest(Name: "Tên khác"));
        Assert.False(renameAttempt.Success);

        var marketing = await service.UpdateCenterProfileAsync(
            center.OwnerUserId!.Value,
            new UpdateCenterProfileRequest(Description: "Mô tả mới", Website: "https://abc.edu.vn", FeeMin: 1000000m, FeeMax: 5000000m));
        Assert.True(marketing.Success);
        Assert.Equal("Mô tả mới", center.Description);
        Assert.Equal("0312345678", center.TaxCode);
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
}

