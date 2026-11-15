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
}

