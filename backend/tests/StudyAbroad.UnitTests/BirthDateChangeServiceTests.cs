using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Application.Common;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-105 / UC-107] Gửi, xem, hủy yêu cầu sửa ngày sinh (E1–E5, 3a, 3b, 5a).</summary>
public class BirthDateChangeServiceTests
{
    // 26/11/2026 12:00 UTC = 19:00 cùng ngày giờ Việt Nam
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = [.. "%PDF-1.4 test"u8.ToArray()];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    internal sealed class FakeStorage : IBirthDateEvidenceStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];
        public List<string> Deleted { get; } = [];

        public Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default)
        {
            var path = $"uploads/birth-date-evidence/{requestId:N}/file{extension}";
            Files[path] = content;
            return Task.FromResult(path);
        }

        public Task<Stream?> OpenAsync(string path, CancellationToken ct = default) =>
            Task.FromResult<Stream?>(Files.TryGetValue(path, out var c) ? new MemoryStream(c) : null);

        public Task DeleteAsync(string? path, CancellationToken ct = default)
        {
            if (path is null) return Task.CompletedTask;
            Files.Remove(path);
            Deleted.Add(path);
            return Task.CompletedTask;
        }

        /// <summary>Giờ tải lên của từng tệp, cho test job dọn; tệp không có giờ coi như vừa tải lên.</summary>
        public Dictionary<string, DateTime> UploadedAt { get; } = [];

        public Task<IReadOnlyList<string>> ListAsync(DateTime uploadedBefore, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Files.Keys
                .Where(p => UploadedAt.TryGetValue(p, out var at) && at < uploadedBefore).ToList());
    }

    internal sealed class FakeRepo : IBirthDateChangeRepository
    {
        public List<BirthDateChangeRequest> Items { get; } = [];
        /// <summary>Giả lập request gửi song song đã ghi trước: lần TryAdd kế tiếp đụng chỉ mục duy nhất.</summary>
        public BirthDateChangeRequest? RaceWinner { get; set; }

        // Trả bản sao như AsNoTracking: TryClose ghi vào dòng trong kho, không đụng đối tượng service đang giữ
        public Task<BirthDateChangeRequest?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(r => r.Id == id).Select(Copy).FirstOrDefault());

        private static BirthDateChangeRequest Copy(BirthDateChangeRequest r) => new()
        {
            Id = r.Id, CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt, UserId = r.UserId, CurrentBirthDate = r.CurrentBirthDate,
            RequestedBirthDate = r.RequestedBirthDate, Reason = r.Reason, Status = r.Status, EvidenceType = r.EvidenceType,
            EvidencePath = r.EvidencePath, EvidenceFileName = r.EvidenceFileName, EvidenceContentType = r.EvidenceContentType,
            EvidenceSize = r.EvidenceSize, RepresentativeEmail = r.RepresentativeEmail, ReviewedByUserId = r.ReviewedByUserId,
            ReviewedAt = r.ReviewedAt, RejectReason = r.RejectReason,
        };

        public Task<IReadOnlyList<string>> GetEvidencePathsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(Items.Where(r => r.EvidencePath != null).Select(r => r.EvidencePath!).ToList());

        public Task<BirthDateChangeRequest?> GetPendingByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Items.FirstOrDefault(r => r.UserId == userId && r.Status == BirthDateChangeStatuses.Pending));

        public Task<BirthDateChangeRequest?> GetLatestByUserAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Items.Where(r => r.UserId == userId).OrderByDescending(r => r.CreatedAt).FirstOrDefault());

        public Task<bool> TryAddAsync(BirthDateChangeRequest request, CancellationToken ct = default)
        {
            if (RaceWinner is { } winner)
            {
                Items.Add(winner);
                RaceWinner = null;
                return Task.FromResult(false);
            }
            if (Items.Any(r => r.UserId == request.UserId && r.Status == BirthDateChangeStatuses.Pending))
                return Task.FromResult(false);
            Items.Add(request);
            return Task.FromResult(true);
        }

        public Task<bool> TryCloseAsync(Guid id, string newStatus, Guid? reviewedByUserId, string? rejectReason, DateTime at, CancellationToken ct = default)
        {
            var r = Items.FirstOrDefault(x => x.Id == id && x.Status == BirthDateChangeStatuses.Pending);
            if (r is null) return Task.FromResult(false);
            r.Status = newStatus;
            r.ReviewedByUserId = reviewedByUserId;
            r.ReviewedAt = reviewedByUserId is null ? null : at;
            r.RejectReason = rejectReason;
            r.EvidencePath = null;
            r.EvidenceFileName = null;
            r.EvidenceContentType = null;
            r.EvidenceSize = null;
            r.UpdatedAt = at;
            return Task.FromResult(true);
        }

        public Task<PagedResult<BirthDateRequestListRow>> SearchAsync(string? status, int page, int pageSize, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private readonly FakeUserRepository _users = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeRepo _repo = new();
    private readonly FakeStorage _storage = new();

    private BirthDateChangeService Service() =>
        new(_repo, _users, _links, _storage, new BirthDateChangeOptions(), new FixedClock(Now));

    private User AddUser(string role, DateOnly? dob, string status = UserStatuses.Active)
    {
        var user = new User { Email = $"{Guid.NewGuid():N}@test.local", Role = role, Status = status, DateOfBirth = dob, FullName = "Người thử" };
        _users.Users.Add(user);
        return user;
    }

    private void GrantConsent(Guid studentId)
    {
        var consent = new Consent { UserId = studentId, Granted = true };
        _links.Consents.Add(consent);
        _links.Links.Add(new ParentStudentLink { StudentUserId = studentId, ParentEmail = "ph@test.local", Status = ParentLinkStatuses.Active, ConsentId = consent.Id });
    }

    private static SubmitBirthDateChangeRequest Req(DateOnly? dob, string? reason = "Nhập nhầm năm sinh",
        string? evidenceType = null, byte[]? file = null, string? rep = null) =>
        new(dob, reason, evidenceType, file is null ? null : new BirthDateEvidenceUpload(file, "giay-khai-sinh.pdf"), rep);

    // ---- luồng chính

    [Fact]
    public async Task Submit_SameAgeBand_NoEvidence_CreatesPending()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1)); // 17 tuổi
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)));

        Assert.True(result.Success);
        Assert.Equal(BirthDateChangeStatuses.Pending, result.Value!.Status);
        Assert.Equal(new DateOnly(2009, 5, 1), result.Value.CurrentBirthDate);
        Assert.False(result.Value.HasEvidence);
        // Ngày sinh trong users chưa đổi cho tới khi Admin duyệt
        Assert.Equal(new DateOnly(2009, 5, 1), _users.Users.Single().DateOfBirth);
        Assert.Single(_repo.Items);
    }

    [Fact]
    public async Task GetCurrent_ReturnsLatestOrNull()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        Assert.Null((await Service().GetCurrentAsync(student.Id)).Value);

        await Service().SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)));
        var current = await Service().GetCurrentAsync(student.Id);
        Assert.Equal(new DateOnly(2009, 6, 1), current.Value!.RequestedBirthDate);
    }

    // ---- quyền

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task Submit_NonStudentParent_Forbidden(string role)
    {
        var user = AddUser(role, new DateOnly(1990, 1, 1));
        var result = await Service().SubmitAsync(user.Id, Req(new DateOnly(1990, 2, 1)));
        Assert.Equal(BirthDateChangeError.Forbidden, result.Error);
    }

    [Fact]
    public async Task Submit_ManagedChild_NotFound()
    {
        var child = AddUser(UserRoles.Student, new DateOnly(2012, 1, 1), UserStatuses.Managed);
        var result = await Service().SubmitAsync(child.Id, Req(new DateOnly(2012, 2, 1)));
        Assert.Equal(BirthDateChangeError.NotFound, result.Error);
    }

    // ---- E1

    [Fact]
    public async Task Submit_E1_InvalidDates()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var parent = AddUser(UserRoles.Parent, new DateOnly(1985, 1, 1));
        var svc = Service();

        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(null))).Error);
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(new DateOnly(2026, 11, 27)))).Error); // tương lai
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 5, 1)))).Error);   // trùng ngày cũ
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(new DateOnly(2014, 1, 1)))).Error);   // 12 tuổi
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(parent.Id, Req(new DateOnly(2009, 1, 1)))).Error);    // phụ huynh 17 tuổi
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(parent.Id, Req(new DateOnly(1925, 1, 1)))).Error);    // quá 100 tuổi
        Assert.Empty(_repo.Items);
    }

    // ---- E2

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Submit_E2_MissingReason(string? reason)
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1), reason));
        Assert.Equal(BirthDateChangeError.Validation, result.Error);
    }

    [Fact]
    public async Task Submit_E2_ReasonTooLong()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1), new string('a', 501)));
        Assert.Equal(BirthDateChangeError.Validation, result.Error);
    }

    // ---- 3a / E3

    [Theory]
    [InlineData(2009, 5, 1, 2008, 5, 1)]  // 17 → 18
    [InlineData(2011, 5, 1, 2010, 5, 1)]  // 15 → 16
    public async Task Submit_E3_CrossingUpWithoutEvidence_Rejected(int oy, int om, int od, int ny, int nm, int nd)
    {
        var student = AddUser(UserRoles.Student, new DateOnly(oy, om, od));
        GrantConsent(student.Id);
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(ny, nm, nd)));
        Assert.Equal(BirthDateChangeError.Validation, result.Error);
        Assert.Contains("minh chứng", result.Message);
    }

    [Fact]
    public async Task Submit_E3_LegacyAccountWithoutDob_TreatedAsCrossing()
    {
        var student = AddUser(UserRoles.Student, null);
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)));
        Assert.Equal(BirthDateChangeError.Validation, result.Error);
    }

    [Fact]
    public async Task Submit_3a_WithValidEvidence_SavesFile()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var result = await Service().SubmitAsync(student.Id,
            Req(new DateOnly(2008, 5, 1), evidenceType: "BIRTH_CERTIFICATE", file: Pdf));

        Assert.True(result.Success);
        Assert.True(result.Value!.HasEvidence);
        Assert.Equal(BirthDateEvidenceTypes.BirthCertificate, result.Value.EvidenceType);
        var saved = _repo.Items.Single();
        Assert.Equal("application/pdf", saved.EvidenceContentType);
        Assert.Equal(Pdf.Length, saved.EvidenceSize);
        Assert.True(_storage.Files.ContainsKey(saved.EvidencePath!));
    }

    [Fact]
    public async Task Submit_E3_BadEvidence_Rejected()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var svc = Service();
        var down = new DateOnly(2008, 5, 1);

        // Loại giấy tờ không có trong danh sách
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(down, evidenceType: "driver_license", file: Pdf))).Error);
        // Đuôi tệp đổi tên nhưng nội dung không phải ảnh / PDF
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(down, evidenceType: "passport", file: "MZ fake exe"u8.ToArray()))).Error);
        // Quá dung lượng
        var big = new byte[new BirthDateChangeOptions().MaxEvidenceBytes + 1];
        Png.CopyTo(big, 0);
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(down, evidenceType: "passport", file: big))).Error);

        Assert.Empty(_repo.Items);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Submit_GoingDownAcrossThreshold_NoEvidenceNeeded()
    {
        var parent = AddUser(UserRoles.Parent, new DateOnly(1985, 1, 1));
        var student = AddUser(UserRoles.Student, new DateOnly(2008, 5, 1)); // 18
        var svc = Service();

        Assert.True((await svc.SubmitAsync(parent.Id, Req(new DateOnly(1990, 1, 1)))).Success);
        Assert.True((await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)))).Success); // 18 → 17
    }

    // ---- 3b / E4

    [Fact]
    public async Task Submit_E4_StudentUnder16WithoutConsent_RequiresRepresentativeEmail()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1)); // 17
        var svc = Service();
        var under16 = new DateOnly(2011, 5, 1);

        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(under16))).Error);
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(under16, rep: "khong-hop-le"))).Error);
        Assert.Equal(BirthDateChangeError.Validation, (await svc.SubmitAsync(student.Id, Req(under16, rep: student.Email.ToUpperInvariant()))).Error);

        var ok = await svc.SubmitAsync(student.Id, Req(under16, rep: "  Me.HocSinh@Test.Local "));
        Assert.True(ok.Success);
        Assert.Equal("me.hocsinh@test.local", ok.Value!.RepresentativeEmail);
    }

    [Fact]
    public async Task Submit_3b_StudentUnder16WithGrantedConsent_NoEmailNeeded()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2011, 5, 1)); // 15
        GrantConsent(student.Id);
        var result = await Service().SubmitAsync(student.Id, Req(new DateOnly(2011, 8, 1)));
        Assert.True(result.Success);
        Assert.Null(result.Value!.RepresentativeEmail);
    }

    // ---- E5

    [Fact]
    public async Task Submit_E5_PendingExists_ConflictWithPendingRequest()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var svc = Service();
        var first = await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)));

        var second = await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 7, 1)));
        Assert.Equal(BirthDateChangeError.Conflict, second.Error);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Single(_repo.Items);
    }

    [Fact]
    public async Task Submit_E5_ConcurrentSubmit_LoserDeletesItsFile()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var winner = new BirthDateChangeRequest
        {
            UserId = student.Id, RequestedBirthDate = new DateOnly(2009, 7, 1), Reason = "lần bấm trước", Status = BirthDateChangeStatuses.Pending,
        };
        _repo.RaceWinner = winner;

        var result = await Service().SubmitAsync(student.Id,
            Req(new DateOnly(2008, 5, 1), evidenceType: "passport", file: Png));

        Assert.Equal(BirthDateChangeError.Conflict, result.Error);
        Assert.Equal(winner.Id, result.Value!.Id);
        Assert.Empty(_storage.Files);
        Assert.Single(_storage.Deleted);
    }

    // ---- 5a: hủy

    [Fact]
    public async Task Cancel_Pending_ClosesAndDeletesEvidence()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var svc = Service();
        var sent = await svc.SubmitAsync(student.Id, Req(new DateOnly(2008, 5, 1), evidenceType: "citizen_id", file: Pdf));

        var cancelled = await svc.CancelAsync(student.Id, sent.Value!.Id);

        Assert.True(cancelled.Success);
        Assert.Equal(BirthDateChangeStatuses.Cancelled, cancelled.Value!.Status);
        Assert.False(cancelled.Value.HasEvidence);
        Assert.Empty(_storage.Files);
        Assert.Null(_repo.Items.Single().ReviewedAt);
        Assert.Null(_repo.Items.Single().EvidenceFileName);   // chỉ còn loại giấy tờ
        Assert.Equal(BirthDateEvidenceTypes.CitizenId, _repo.Items.Single().EvidenceType);

        // Hủy rồi thì gửi được yêu cầu mới
        Assert.True((await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)))).Success);
    }

    [Fact]
    public async Task Cancel_OtherUsersRequest_NotFound()
    {
        var owner = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var other = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var svc = Service();
        var sent = await svc.SubmitAsync(owner.Id, Req(new DateOnly(2009, 6, 1)));

        Assert.Equal(BirthDateChangeError.NotFound, (await svc.CancelAsync(other.Id, sent.Value!.Id)).Error);
        Assert.Equal(BirthDateChangeStatuses.Pending, _repo.Items.Single().Status);
    }

    [Fact]
    public async Task Cancel_AlreadyClosed_Conflict()
    {
        var student = AddUser(UserRoles.Student, new DateOnly(2009, 5, 1));
        var svc = Service();
        var sent = await svc.SubmitAsync(student.Id, Req(new DateOnly(2009, 6, 1)));
        await svc.CancelAsync(student.Id, sent.Value!.Id);

        Assert.Equal(BirthDateChangeError.Conflict, (await svc.CancelAsync(student.Id, sent.Value.Id)).Error);
    }
}
