using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.StudyCenters;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class StudyCenterServiceTests
{
    private sealed class FakeRepository : IStudyCenterRepository
    {
        public List<StudyCenter> Centers { get; } = [];

        public FakeRepository(params StudyCenter[] initial)
        {
            Centers.AddRange(initial);
        }

        public StudyCenterQuery? LastQuery { get; private set; }

        public Task<PagedResult<StudyCenter>> SearchAsync(StudyCenterQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<StudyCenter>(Centers, query.Page, query.PageSize, Centers.Count));
        }

        public Task<StudyCenter?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<StudyCenter?> GetByIdForUpdateAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<StudyCenter>> ListReferableAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<StudyCenter>>(Centers);

        public Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(Centers.Any(c => c.Code.Equals(code, StringComparison.OrdinalIgnoreCase)));

        public Task AddAsync(StudyCenter center, CancellationToken ct = default)
        {
            Centers.Add(center);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(StudyCenter center, CancellationToken ct = default)
        {
            var idx = Centers.FindIndex(c => c.Id == center.Id);
            if (idx >= 0)
            {
                Centers[idx] = center;
            }
            return Task.CompletedTask;
        }

        public Task<StudyCenter?> GetByOwnerOrEmailAsync(Guid ownerUserId, string? email, CancellationToken ct = default) =>
            Task.FromResult(Centers.FirstOrDefault(c => c.OwnerUserId == ownerUserId || (email != null && string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase))));
    }

    private readonly FakeAuditLogRepository _auditLogs = new();

    [Fact]
    public async Task SearchAsync_ClampsPagingValues()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        await service.SearchAsync(new StudyCenterQuery(Page: 0, PageSize: 1000));

        Assert.Equal(1, repo.LastQuery!.Page);
        Assert.Equal(100, repo.LastQuery.PageSize);
    }

    [Fact]
    public async Task GetByIdAsync_MapsEntityToDto()
    {
        var center = new StudyCenter { Code = "CTY_001", Name = "Test", Services = ["CHON_TRUONG"] };
        var service = new StudyCenterService(new FakeRepository(center), _auditLogs);

        var dto = await service.GetByIdAsync(center.Id);

        Assert.NotNull(dto);
        Assert.Equal("CTY_001", dto.Code);
        Assert.Contains("CHON_TRUONG", dto.Services);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenMissing()
    {
        var service = new StudyCenterService(new FakeRepository(), _auditLogs);
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesCenterAndWritesAudit()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var request = new CreateStudyCenterRequest(
            Code: "CTY_NEW",
            Name: "Trung tâm Du học Mới",
            City: "Hà Nội",
            Address: "123 Cầu Giấy",
            Website: "https://example.com",
            Phone: "0912345678",
            Services: ["CHON_TRUONG", "LUYEN_PHONG_VAN"],
            StudyLevels: ["dai_hoc", "thac_si"]);

        var result = await service.CreateAsync(request, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("CTY_NEW", result.Center.Code);
        Assert.Equal("Trung tâm Du học Mới", result.Center.Name);
        Assert.Equal("unverified", result.Center.VerificationStatus);
        Assert.True(result.Center.IsActive);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.create", log.Action);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_ReturnsFail()
    {
        var existing = new StudyCenter { Code = "DUPLICATE_CODE", Name = "Trung tâm cũ" };
        var repo = new FakeRepository(existing);
        var service = new StudyCenterService(repo, _auditLogs);

        var request = new CreateStudyCenterRequest(
            Code: "duplicate_code",
            Name: "Trung tâm trùng mã");

        var result = await service.CreateAsync(request, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("đã tồn tại", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Theory]
    [InlineData("", "Tên hợp lệ", "Mã trung tâm")]
    [InlineData("CODE", "", "Tên trung tâm")]
    public async Task CreateAsync_EmptyRequiredFields_ReturnsFail(string code, string name, string expectedError)
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.CreateAsync(new CreateStudyCenterRequest(code, name), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains(expectedError, result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task CreateAsync_WithEnhancedFields_MapsPropertiesCorrectly()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var request = new CreateStudyCenterRequest(
            Code: "CTY_013",
            Name: "Summit Education Services",
            City: "Hà Nội",
            District: "Đống Đa",
            Address: "101 Láng Hạ",
            Website: "https://summit.edu.vn",
            Phone: "0901702889",
            Email: "contact@summit.edu.vn",
            CenterType: "hybrid",
            LogoUrl: "https://example.com/logo.png",
            CoverImageUrl: "https://example.com/cover.png",
            GalleryImages: ["https://example.com/img1.png", "https://example.com/img2.png"],
            StaffCount: 65,
            EstablishedYear: 2010,
            LicenseNumber: "GPHĐ-GD-2010/HN-088",
            Accreditations: ["AIRC", "ICEF"],
            VisaSuccessRate: 99.2,
            Services: ["LUYEN_THI_SAT", "SAN_HOC_BONG"]);

        var result = await service.CreateAsync(request, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("hybrid", result.Center.CenterType);
        Assert.Equal("Đống Đa", result.Center.District);
        Assert.Equal(65, result.Center.StaffCount);
        Assert.Equal(2010, result.Center.EstablishedYear);
        Assert.Equal("GPHĐ-GD-2010/HN-088", result.Center.LicenseNumber);
        Assert.Equal(99.2, result.Center.VisaSuccessRate);
        Assert.Equal(2, result.Center.GalleryImages?.Count);
        Assert.Equal(2, result.Center.Accreditations?.Count);
    }

    [Fact]
    public void StudyCentersController_ActionAttributes_MatchRbacPolicy()
    {
        // UC-71: Thêm trung tâm vào danh bạ - Admin hoặc Moderator được phép
        var createAttr = typeof(StudyCentersController).GetMethod(nameof(StudyCentersController.Create))?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAttr);
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", createAttr.Roles);

        // UC-64: Tạo tài khoản đại diện trung tâm - Admin hoặc Moderator được phép
        var createAccountAttr = typeof(StudyCentersController).GetMethod(nameof(StudyCentersController.CreateCenterAccount))?.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(createAccountAttr);
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", createAccountAttr.Roles);
    }

    [Fact]
    public async Task VerifyCenterAsync_VerifyCenter_SetsStatusVerified_AndLogsAudit()
    {
        var center = new StudyCenter
        {
            Code = "CENTER_TEST",
            Name = "Trung tâm Khảo sát",
            VerificationStatus = "unverified"
        };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var result = await service.VerifyCenterAsync(center.Id, true, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("verified", result.Center.VerificationStatus);
        Assert.NotNull(result.Center.VerifiedAt);
        Assert.Equal(actorId, result.Center.VerifiedByUserId);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.verify", log.Action);
        Assert.Equal("study_center", log.EntityType);
        Assert.Equal(center.Id.ToString(), log.EntityId);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task VerifyCenterAsync_UnverifyCenter_SetsStatusUnverified_ClearsVerificationInfo_AndLogsAudit()
    {
        var center = new StudyCenter
        {
            Code = "CENTER_TEST_VERIFIED",
            Name = "Trung tâm Đã Xác minh",
            VerificationStatus = "verified",
            VerifiedAt = DateTime.UtcNow.AddDays(-10),
            VerifiedByUserId = Guid.NewGuid()
        };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var result = await service.VerifyCenterAsync(center.Id, false, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("unverified", result.Center.VerificationStatus);
        Assert.Null(result.Center.VerifiedAt);
        Assert.Null(result.Center.VerifiedByUserId);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.unverify", log.Action);
        Assert.Equal("study_center", log.EntityType);
        Assert.Equal(center.Id.ToString(), log.EntityId);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task VerifyCenterAsync_NonExistentCenter_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var nonExistentId = Guid.NewGuid();

        var result = await service.VerifyCenterAsync(nonExistentId, true, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task UpdateCenterAsync_ValidRequest_UpdatesCenterAndWritesAudit()
    {
        var center = new StudyCenter
        {
            Code = "CENTER_UPDATE",
            Name = "Tên Trung Tâm Cũ",
            City = "Hà Nội",
            District = "Ba Đình",
            Address = "12 Kim Mã",
            Phone = "0911111111",
            Email = "old@example.com",
            Website = "https://old.example.com",
            CenterType = "study_abroad",
            StaffCount = 20,
            EstablishedYear = 2015,
            LicenseNumber = "OLD_LICENSE_01",
            VisaSuccessRate = 95.0,
            StudyLevels = ["dai_hoc"],
            Services = ["CHON_TRUONG"]
        };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var request = new UpdateStudyCenterRequest(
            Name: "Tên Trung Tâm Mới",
            City: "TP.HCM",
            District: "Quận 1",
            Address: "45 Lê Duẩn",
            Phone: "0988888888",
            Email: "new@example.com",
            Website: "https://new.example.com",
            Description: "Mô tả cập nhật mới",
            CenterType: "hybrid",
            LogoUrl: "https://example.com/new-logo.png",
            CoverImageUrl: "https://example.com/new-cover.png",
            GalleryImages: ["https://example.com/gallery1.png"],
            StaffCount: 50,
            EstablishedYear: 2012,
            LicenseNumber: "NEW_LICENSE_888",
            Accreditations: ["AIRC", "ICEF"],
            VisaSuccessRate: 99.5,
            StudyLevels: ["dai_hoc", "thac_si"],
            Services: ["CHON_TRUONG", "LUYEN_PHONG_VAN"]);

        var result = await service.UpdateCenterAsync(center.Id, request, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.Equal("Tên Trung Tâm Mới", result.Center.Name);
        Assert.Equal("TP.HCM", result.Center.City);
        Assert.Equal("Quận 1", result.Center.District);
        Assert.Equal("45 Lê Duẩn", result.Center.Address);
        Assert.Equal("0988888888", result.Center.Phone);
        Assert.Equal("new@example.com", result.Center.Email);
        Assert.Equal("https://new.example.com", result.Center.Website);
        Assert.Equal("hybrid", result.Center.CenterType);
        Assert.Equal(50, result.Center.StaffCount);
        Assert.Equal(2012, result.Center.EstablishedYear);
        Assert.Equal("NEW_LICENSE_888", result.Center.LicenseNumber);
        Assert.Equal(99.5, result.Center.VisaSuccessRate);
        Assert.Contains("thac_si", result.Center.StudyLevels);
        Assert.Contains("LUYEN_PHONG_VAN", result.Center.Services);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.update", log.Action);
        Assert.Equal("study_center", log.EntityType);
        Assert.Equal(center.Id.ToString(), log.EntityId);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task UpdateCenterAsync_EmptyName_ReturnsFail()
    {
        var center = new StudyCenter { Code = "CTY_EMPTY", Name = "Hợp lệ" };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.UpdateCenterAsync(center.Id, new UpdateStudyCenterRequest(Name: "  "), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("không được để trống", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task UpdateCenterAsync_NonExistentCenter_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.UpdateCenterAsync(Guid.NewGuid(), new UpdateStudyCenterRequest(Name: "Trung tâm ảo"), Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task SetCenterActiveAsync_HideCenter_SetsIsActiveFalseAndWritesAudit()
    {
        var center = new StudyCenter { Code = "CTY_ACTIVE", Name = "Đang Hoạt Động", IsActive = true };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var result = await service.SetCenterActiveAsync(center.Id, false, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.False(result.Center.IsActive);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.hide", log.Action);
        Assert.Equal("study_center", log.EntityType);
        Assert.Equal(center.Id.ToString(), log.EntityId);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task SetCenterActiveAsync_ShowCenter_SetsIsActiveTrueAndWritesAudit()
    {
        var center = new StudyCenter { Code = "CTY_HIDDEN", Name = "Đang Ẩn", IsActive = false };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var result = await service.SetCenterActiveAsync(center.Id, true, actorId);

        Assert.True(result.Success);
        Assert.NotNull(result.Center);
        Assert.True(result.Center.IsActive);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("study_center.show", log.Action);
        Assert.Equal("study_center", log.EntityType);
        Assert.Equal(center.Id.ToString(), log.EntityId);
        Assert.Equal(actorId, log.ActorUserId);
    }

    [Fact]
    public async Task SetCenterActiveAsync_NonExistentCenter_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.SetCenterActiveAsync(Guid.NewGuid(), false, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.ErrorMessage);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task CreateAsync_WithLegalFields_TrimsValidatesAndMapsToDto()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();
        var issuedDate = new DateOnly(2024, 5, 10);

        var request = new CreateStudyCenterRequest(
            Code: "CTY_LEGAL",
            Name: "Trung tâm Pháp lý Đầy đủ",
            CenterType: "hybrid",
            StaffCount: 20,
            LicenseNumber: "  GPHĐ-2024/HN  ",
            TaxCode: "  0109876543 ",
            LegalRepresentative: "  Nguyễn Minh Quân  ",
            LicenseIssuedBy: "  Sở GD&ĐT Hà Nội ",
            LicenseIssuedDate: issuedDate,
            LegalComplianceMode: " NOTIFIED ",
            TrainingLicenseNumber: " GPĐT-NN-88 ",
            CertifiedCounselorCount: 12,
            LegalDocumentUrls: [" https://example.com/dkkd.pdf ", "/uploads/gp.pdf", "https://example.com/dkkd.pdf", ""],
            VerificationNote: "  Đã nộp thông báo hoạt động  ");

        var result = await service.CreateAsync(request, actorId);

        Assert.True(result.Success);
        var dto = result.Center!;
        Assert.Equal("GPHĐ-2024/HN", dto.LicenseNumber);
        Assert.Equal("0109876543", dto.TaxCode);
        Assert.Equal("Nguyễn Minh Quân", dto.LegalRepresentative);
        Assert.Equal("Sở GD&ĐT Hà Nội", dto.LicenseIssuedBy);
        Assert.Equal(issuedDate, dto.LicenseIssuedDate);
        Assert.Equal("notified", dto.LegalComplianceMode);
        Assert.Equal("GPĐT-NN-88", dto.TrainingLicenseNumber);
        Assert.Equal(12, dto.CertifiedCounselorCount);
        Assert.Equal(["https://example.com/dkkd.pdf", "/uploads/gp.pdf"], dto.LegalDocumentUrls);
        Assert.Equal("Đã nộp thông báo hoạt động", dto.VerificationNote);
    }

    [Fact]
    public async Task CreateAsync_InvalidComplianceMode_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.CreateAsync(
            new CreateStudyCenterRequest(Code: "CTY_BAD_MODE", Name: "Bad Mode", LegalComplianceMode: "self_declared"),
            Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("licensed", result.ErrorMessage);
        Assert.Empty(repo.Centers);
    }

    [Fact]
    public async Task CreateAsync_CertifiedCounselorsExceedStaff_OrNegative_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var negativeResult = await service.CreateAsync(
            new CreateStudyCenterRequest(Code: "CTY_NEG", Name: "Negative", CertifiedCounselorCount: -1),
            Guid.NewGuid());
        Assert.False(negativeResult.Success);
        Assert.Contains("không được âm", negativeResult.ErrorMessage);

        var exceedResult = await service.CreateAsync(
            new CreateStudyCenterRequest(Code: "CTY_EXC", Name: "Exceed", StaffCount: 5, CertifiedCounselorCount: 6),
            Guid.NewGuid());
        Assert.False(exceedResult.Success);
        Assert.Contains("tổng nhân sự", exceedResult.ErrorMessage);
    }

    [Fact]
    public async Task CreateAsync_InvalidLegalDocumentUrl_ReturnsFail()
    {
        var repo = new FakeRepository();
        var service = new StudyCenterService(repo, _auditLogs);

        var result = await service.CreateAsync(
            new CreateStudyCenterRequest(Code: "CTY_BAD_URL", Name: "Bad URL", LegalDocumentUrls: ["javascript:alert(1)"]),
            Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("không hợp lệ", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateCenterAsync_UpdatesLegalFields_AndPreservesNoteWhenNull()
    {
        var center = new StudyCenter
        {
            Code = "CTY_UPD_LEGAL",
            Name = "Trung tâm Cũ",
            LegalComplianceMode = "licensed",
            LegalDocumentUrls = ["/uploads/existing.pdf"],
            VerificationNote = "Ghi chú kiểm duyệt cũ"
        };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);

        var request = new UpdateStudyCenterRequest(
            Name: "Trung tâm Mới",
            StaffCount: 30,
            TaxCode: "0312345678",
            LegalRepresentative: "Lê Thị Hồng",
            LicenseIssuedBy: "Sở GD&ĐT TP.HCM",
            LicenseIssuedDate: new DateOnly(2025, 2, 1),
            LegalComplianceMode: "notified",
            TrainingLicenseNumber: "GPĐT-HCM-99",
            CertifiedCounselorCount: 15);

        var result = await service.UpdateCenterAsync(center.Id, request, Guid.NewGuid());

        Assert.True(result.Success);
        var dto = result.Center!;
        Assert.Equal("0312345678", dto.TaxCode);
        Assert.Equal("Lê Thị Hồng", dto.LegalRepresentative);
        Assert.Equal("Sở GD&ĐT TP.HCM", dto.LicenseIssuedBy);
        Assert.Equal(new DateOnly(2025, 2, 1), dto.LicenseIssuedDate);
        Assert.Equal("notified", dto.LegalComplianceMode);
        Assert.Equal("GPĐT-HCM-99", dto.TrainingLicenseNumber);
        Assert.Equal(15, dto.CertifiedCounselorCount);
        Assert.Equal(["/uploads/existing.pdf"], dto.LegalDocumentUrls);
        Assert.Equal("Ghi chú kiểm duyệt cũ", dto.VerificationNote);
    }

    [Fact]
    public async Task VerifyCenterAsync_WithVerificationNote_UpdatesNoteAndLogsAudit()
    {
        var center = new StudyCenter { Code = "CTY_NOTE", Name = "Trung tâm Xác minh" };
        var repo = new FakeRepository(center);
        var service = new StudyCenterService(repo, _auditLogs);
        var actorId = Guid.NewGuid();

        var verifyRes = await service.VerifyCenterAsync(center.Id, true, actorId, "  Đã kiểm tra GCN ĐKDN và GP Sở GD&ĐT  ");
        Assert.True(verifyRes.Success);
        Assert.Equal("Đã kiểm tra GCN ĐKDN và GP Sở GD&ĐT", verifyRes.Center!.VerificationNote);
        using var doc = System.Text.Json.JsonDocument.Parse(_auditLogs.Entries.Single().DetailJson!);
        Assert.Equal("Đã kiểm tra GCN ĐKDN và GP Sở GD&ĐT", doc.RootElement.GetProperty("verificationNote").GetString());

        var tooLongRes = await service.VerifyCenterAsync(center.Id, false, actorId, new string('x', 1001));
        Assert.False(tooLongRes.Success);
        Assert.Contains("tối đa", tooLongRes.ErrorMessage);
    }
}
