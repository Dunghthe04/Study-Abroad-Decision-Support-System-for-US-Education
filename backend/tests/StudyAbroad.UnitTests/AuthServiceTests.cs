using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class AuthServiceTests
{
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private sealed class FakeTokenService : ITokenService
    {
        public (string Token, DateTime ExpiresAt) GenerateToken(User user) =>
            ("token:" + user.Email, DateTime.UtcNow.AddDays(7));

        public string HashToken(string token) => "hash:" + token;
    }

    private readonly FakeUserRepository _repo = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeParentLinkRepository _links = new();
    private readonly FakeAuditLogRepository _auditLogs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeLeadWithdrawalRepository _leadWithdrawals = new();

    private AuthService CreateService(AccountDeletionOptions? accountDeletion = null) => new(
        _repo, new FakeHasher(), _tokens, _emailSender, _links, _auditLogs,
        new ParentLinkService(_links, _repo, _emailSender, _auditLogs), _unitOfWork, _leadWithdrawals,
        accountDeletion: accountDeletion);

    // Ngày sinh ứng với tuổi tròn (sinh nhật cách hôm nay ở VN 10 ngày về trước)
    private static DateOnly BornYearsAgo(int years) =>
        AgePolicy.TodayInVietnam(DateTime.UtcNow).AddDays(-10).AddYears(-years);

    private static RegisterRequest Request(
        string email = "a@test.com",
        string password = "password123",
        string fullName = "Nguyen Van A",
        string role = UserRoles.Parent,
        DateOnly? dateOfBirth = null,
        string? parentEmail = null,
        string? phone = null,
        bool noDateOfBirth = false) =>
        new(email, password, fullName, role, noDateOfBirth ? null : dateOfBirth ?? BornYearsAgo(30), parentEmail, phone);

    [Theory]
    [InlineData(UserRoles.Parent, 30, null)]
    [InlineData(UserRoles.Student, 18, null)]
    [InlineData(UserRoles.Student, 17, null)]
    [InlineData(UserRoles.Student, 15, "parent@test.com")]
    public async Task Register_StudentOrParent_IsUnverifiedAndSendsOtpEmail(string role, int age, string? parentEmail)
    {
        var result = await CreateService().RegisterAsync(Request(role: role, dateOfBirth: BornYearsAgo(age), parentEmail: parentEmail));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Null(result.User);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
        Assert.Single(_repo.Users);
        Assert.Single(_emailSender.SentEmails);
        Assert.Equal(OtpPurposes.VerifyEmail, _emailSender.SentEmails[0].Purpose);
    }

    [Fact]
    public async Task VerifyEmail_ValidOtp_ActivatesAccountAndReturnsAuthResponse()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var otpCode = _emailSender.SentEmails[0].Code;

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", otpCode));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(UserStatuses.Active, _repo.Users[0].Status);
    }

    // [UC-75 / UC-115] Phụ huynh đã đồng ý qua link mail, sau đó đăng ký bằng đúng email: xác thực OTP xong là thấy con
    [Fact]
    public async Task VerifyEmail_Parent_LinksConsentGivenByEmail()
    {
        var kid = new User { Email = "kid@test.com", FullName = "Kid", Role = UserRoles.Student, Status = UserStatuses.Active, PasswordHash = "x" };
        _repo.Users.Add(kid);
        var link = new ParentStudentLink { StudentUserId = kid.Id, ParentEmail = "a@test.com", Status = ParentLinkStatuses.Active };
        _links.Links.Add(link);
        var service = CreateService();
        await service.RegisterAsync(Request(email: "A@test.com"));
        var otpCode = _emailSender.SentEmails[0].Code;

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", otpCode));

        Assert.Equal(AuthError.None, result.Error);
        var parent = _repo.Users.Single(u => u.Role == UserRoles.Parent);
        Assert.Equal(parent.Id, _links.Links.Single().ParentUserId);
        Assert.Contains(_emailSender.SentEmails, e => e.To == kid.Email && e.Purpose == "parent_account_linked");
    }

    [Fact]
    public async Task VerifyEmail_ExpiredOtp_ReturnsGenericOtpInvalid()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var otp = _repo.Otps[0];
        otp.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // Hết hạn

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", _emailSender.SentEmails[0].Code));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(AuthService.OtpFailedMessage, result.Message);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
    }

    [Fact]
    public async Task VerifyEmail_WrongOtp_IncrementsAttemptsAndReturnsError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(1, _repo.Otps[0].Attempts);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
    }

    [Fact]
    public async Task Register_SavesDateOfBirth()
    {
        var dob = BornYearsAgo(30);
        await CreateService().RegisterAsync(Request(dateOfBirth: dob));

        Assert.Equal(dob, _repo.Users[0].DateOfBirth);
    }

    [Fact]
    public async Task Register_WithoutDateOfBirth_IsValidationError()
    {
        var result = await CreateService().RegisterAsync(Request(noDateOfBirth: true));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Register_DateOfBirthInFuture_IsValidationError()
    {
        var tomorrow = AgePolicy.TodayInVietnam(DateTime.UtcNow).AddDays(1);

        var result = await CreateService().RegisterAsync(Request(dateOfBirth: tomorrow));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_AgeOverMax_IsValidationError()
    {
        var result = await CreateService().RegisterAsync(Request(dateOfBirth: BornYearsAgo(AuthService.MaxAge + 1)));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(10)]
    public async Task Register_ParentUnder18_IsValidationError(int age)
    {
        var result = await CreateService().RegisterAsync(Request(role: UserRoles.Parent, dateOfBirth: BornYearsAgo(age)));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_StudentUnder16WithoutParentEmail_IsValidationError(string? parentEmail)
    {
        var result = await CreateService().RegisterAsync(
            Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(15), parentEmail: parentEmail));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
        Assert.Empty(_links.Links);
    }

    [Theory]
    [InlineData("a@test.com")]
    [InlineData(" A@Test.com ")]
    [InlineData("not-an-email")]
    public async Task Register_StudentInvalidOrSameParentEmail_IsValidationError(string parentEmail)
    {
        var result = await CreateService().RegisterAsync(
            Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(15), parentEmail: parentEmail));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_Student16To17WithoutParentEmail_SucceedsWithoutLink()
    {
        var result = await CreateService().RegisterAsync(Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(16)));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Single(_repo.Users);
        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task Register_Student16To17WithParentEmail_CreatesLink()
    {
        await CreateService().RegisterAsync(
            Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(17), parentEmail: "Parent@Test.com"));

        var link = Assert.Single(_links.Links);
        Assert.Equal("parent@test.com", link.ParentEmail);
    }

    [Fact]
    public async Task Register_AdultStudentParentEmail_IsIgnored()
    {
        var result = await CreateService().RegisterAsync(
            Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(19), parentEmail: "parent@test.com"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Empty(_links.Links);
    }

    [Fact]
    public async Task Register_StudentUnder16_InviteSentOnlyAfterEmailVerified()
    {
        var service = CreateService();
        await service.RegisterAsync(
            Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(14), parentEmail: "parent@test.com"));

        // Trước khi xác thực: đã lưu link, chưa có token, chưa gửi mail mời
        var link = Assert.Single(_links.Links);
        Assert.Equal(_repo.Users[0].Id, link.StudentUserId);
        Assert.Equal(ParentLinkStatuses.Pending, link.Status);
        Assert.Null(link.TokenHash);
        Assert.Equal(0, link.InviteSentCount);
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.Purpose == "parent_invite");

        var otpCode = _emailSender.SentEmails[0].Code;
        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", otpCode));

        Assert.Equal(AuthError.None, result.Error);
        var invite = Assert.Single(_emailSender.SentEmails, e => e.Purpose == "parent_invite");
        Assert.Equal("parent@test.com", invite.To);
        link = _links.Links[0];
        Assert.Equal(ParentInvite.HashToken(invite.Code), link.TokenHash);
        Assert.NotEqual(invite.Code, link.TokenHash);
        Assert.Equal(1, link.InviteSentCount);
        Assert.NotNull(link.LastInviteSentAt);
        Assert.InRange(link.TokenExpiresAt!.Value,
            DateTime.UtcNow.AddDays(ParentInvite.ExpiryDays).AddMinutes(-1),
            DateTime.UtcNow.AddDays(ParentInvite.ExpiryDays).AddMinutes(1));
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Center)]
    [InlineData(UserRoles.Moderator)]
    [InlineData("teacher")]
    [InlineData("")]
    public async Task Register_RoleNotAllowed_IsValidationError(string role)
    {
        var result = await CreateService().RegisterAsync(Request(role: role));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsSameMessageAndMailsOwner()
    {
        var service = CreateService();
        var first = await service.RegisterAsync(Request());

        var result = await service.RegisterAsync(Request(email: " A@Test.com "));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(first.Message, result.Message);
        Assert.Single(_repo.Users);
        Assert.Single(_repo.Otps); // không sinh OTP mới
        Assert.Equal(("a@test.com", "account_exists_notice"), (_emailSender.SentEmails[^1].To, _emailSender.SentEmails[^1].Purpose));
    }

    [Fact]
    public async Task CreateModeratorAccount_CreatesActiveModeratorWithoutOtp()
    {
        var service = CreateService();
        var result = await service.CreateModeratorAccountAsync(
            new CreateModeratorAccountRequest("mod@test.com", "password123", "Người kiểm duyệt", "0912345678"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.User);
        Assert.Equal(UserRoles.Moderator, result.User.Role);
        Assert.Equal(UserStatuses.Active, result.User.Status);
        var stored = Assert.Single(_repo.Users);
        Assert.Equal("mod@test.com", stored.Email);
        Assert.Empty(_repo.Otps);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task CreateModeratorAccount_DuplicateEmail_ReturnsValidationError()
    {
        var service = CreateService();
        await service.CreateModeratorAccountAsync(
            new CreateModeratorAccountRequest("mod@test.com", "password123", "Mod A"));
        var result = await service.CreateModeratorAccountAsync(
            new CreateModeratorAccountRequest("MOD@test.com", "password123", "Mod B"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Login_ModeratorCreatedByAdmin_SucceedsWithoutEmailVerification()
    {
        var service = CreateService();
        await service.CreateModeratorAccountAsync(
            new CreateModeratorAccountRequest("mod@test.com", "password123", "Mod User"));

        var login = await service.LoginAsync(new LoginRequest("mod@test.com", "password123"));

        Assert.Equal(AuthError.None, login.Error);
        Assert.NotNull(login.Response);
        Assert.Equal(UserRoles.Moderator, login.Response.User.Role);
    }

    // Refresh token: đăng nhập trả kèm refresh token, phiên lưu hash của nó và hết hạn sau 7 ngày (không theo access token)
    [Fact]
    public async Task Login_ReturnsRefreshToken_SessionStoresItsHashAndLasts7Days()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        var response = login.Response!;
        Assert.False(string.IsNullOrEmpty(response.RefreshToken));
        Assert.NotEqual(response.AccessToken, response.RefreshToken);

        var session = Assert.Single(_repo.Sessions);
        Assert.Equal(_tokens.HashToken(response.RefreshToken!), session.RefreshTokenHash);
        Assert.Equal(_tokens.HashToken(response.AccessToken), session.TokenHash);
        Assert.Null(session.PreviousRefreshTokenHash);
        Assert.Equal(session.ExpiresAt, response.SessionExpiresAt);
        Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddDays(7).AddMinutes(-1), DateTime.UtcNow.AddDays(7).AddMinutes(1));
    }

    // [UC-03] AUTH-215: phiên Moderator chỉ sống 8 giờ tính từ lúc đăng nhập, cookie theo cùng mốc
    [Fact]
    public async Task Login_Moderator_SessionLasts8Hours()
    {
        var service = CreateService();
        await service.CreateModeratorAccountAsync(
            new CreateModeratorAccountRequest("mod@test.com", "password123", "Mod User"));

        var login = await service.LoginAsync(new LoginRequest("mod@test.com", "password123"));

        var session = Assert.Single(_repo.Sessions);
        Assert.Equal(session.ExpiresAt, login.Response!.SessionExpiresAt);
        Assert.InRange(session.ExpiresAt, DateTime.UtcNow.AddHours(8).AddMinutes(-1), DateTime.UtcNow.AddHours(8).AddMinutes(1));
    }

    [Theory]
    [InlineData(UserRoles.Student, 7 * 24)]
    [InlineData(UserRoles.Parent, 7 * 24)]
    [InlineData(UserRoles.Center, 7 * 24)]
    [InlineData(UserRoles.Moderator, 8)]
    [InlineData(UserRoles.Admin, 8)]
    public void LifetimeFor_FollowsRole(string role, int hours) =>
        Assert.Equal(TimeSpan.FromHours(hours), RefreshTokens.LifetimeFor(role));

    [Fact]
    public async Task LockUserAccount_LocksStudent_WritesAuditAndRevokesSessions()
    {
        var adminId = Guid.NewGuid();
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;
        var studentId = _repo.Users[0].Id;
        await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Single(_repo.Sessions);
        Assert.False(_repo.Sessions[0].IsRevoked);

        var result = await service.LockUserAccountAsync(
            studentId,
            new LockUserAccountRequest("Vi phạm quy chế cộng đồng"),
            adminId);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Locked, _repo.Users[0].Status);
        Assert.True(_repo.Sessions[0].IsRevoked);
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("user.lock", log.Action);
        Assert.Equal(adminId, log.ActorUserId);
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "account_locked_notice");
    }

    [Fact]
    public async Task LockUserAccount_AdminTarget_ReturnsValidationError()
    {
        var service = CreateService();
        _repo.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            Role = UserRoles.Admin,
            Status = UserStatuses.Active,
            PasswordHash = "x",
            FullName = "Admin",
        });
        var adminTargetId = _repo.Users[0].Id;

        var result = await service.LockUserAccountAsync(
            adminTargetId,
            new LockUserAccountRequest("test"),
            Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task LockUserAccount_AlreadyLocked_ReturnsValidationError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var studentId = _repo.Users[0].Id;
        _repo.Users[0].Status = UserStatuses.Locked;

        var result = await service.LockUserAccountAsync(
            studentId,
            new LockUserAccountRequest("lý do"),
            Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Fact]
    public async Task UnlockUserAccount_UnlocksLockedUser_WritesAuditAndResetsLockout()
    {
        var adminId = Guid.NewGuid();
        var service = CreateService();
        await service.RegisterAsync(Request());
        var studentId = _repo.Users[0].Id;
        _repo.Users[0].Status = UserStatuses.Locked;
        _repo.Users[0].FailedLoginAttempts = 5;
        _repo.Users[0].LockoutEnd = DateTime.UtcNow.AddMinutes(15);

        var result = await service.UnlockUserAccountAsync(
            studentId,
            new UnlockUserAccountRequest("Đã xác minh thông tin giải trình"),
            adminId);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Active, _repo.Users[0].Status);
        Assert.Equal(0, _repo.Users[0].FailedLoginAttempts);
        Assert.Null(_repo.Users[0].LockoutEnd);
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("user.unlock", log.Action);
        Assert.Equal(adminId, log.ActorUserId);
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "account_unlocked_notice");
    }

    [Fact]
    public async Task UnlockUserAccount_WhenNotLocked_ReturnsValidationError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var studentId = _repo.Users[0].Id;
        _repo.Users[0].Status = UserStatuses.Active;

        var result = await service.UnlockUserAccountAsync(
            studentId,
            new UnlockUserAccountRequest("lý do"),
            Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task UnlockUserAccount_EmptyReason_ReturnsValidationError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var studentId = _repo.Users[0].Id;
        _repo.Users[0].Status = UserStatuses.Locked;

        var result = await service.UnlockUserAccountAsync(
            studentId,
            new UnlockUserAccountRequest("   "),
            Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task LockUserAccount_ModeratorActor_CanLockSubordinatesOnly()
    {
        var service = CreateService();
        var moderatorActor = new User
        {
            Id = Guid.NewGuid(),
            Email = "mod1@test.com",
            Role = UserRoles.Moderator,
            Status = UserStatuses.Active,
            FullName = "Moderator Actor",
            PasswordHash = "x",
        };
        var otherMod = new User
        {
            Id = Guid.NewGuid(),
            Email = "mod2@test.com",
            Role = UserRoles.Moderator,
            Status = UserStatuses.Active,
            FullName = "Other Moderator",
            PasswordHash = "x",
        };
        var adminTarget = new User
        {
            Id = Guid.NewGuid(),
            Email = "admin@test.com",
            Role = UserRoles.Admin,
            Status = UserStatuses.Active,
            FullName = "Admin Target",
            PasswordHash = "x",
        };
        var centerTarget = new User
        {
            Id = Guid.NewGuid(),
            Email = "center@test.com",
            Role = UserRoles.Center,
            Status = UserStatuses.Active,
            FullName = "Center Target",
            PasswordHash = "x",
        };
        var studentTarget = new User
        {
            Id = Guid.NewGuid(),
            Email = "student@test.com",
            Role = UserRoles.Student,
            Status = UserStatuses.Active,
            FullName = "Student Target",
            PasswordHash = "x",
        };
        _repo.Users.AddRange([moderatorActor, otherMod, adminTarget, centerTarget, studentTarget]);

        // Moderator cannot lock another Moderator
        var modLockResult = await service.LockUserAccountAsync(otherMod.Id, new LockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.Validation, modLockResult.Error);
        Assert.Contains("Kiểm duyệt viên không có quyền", modLockResult.Message);

        // Moderator cannot lock Admin
        var adminLockResult = await service.LockUserAccountAsync(adminTarget.Id, new LockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.Validation, adminLockResult.Error);

        // Moderator CAN lock Center (subordinate)
        var centerLockResult = await service.LockUserAccountAsync(centerTarget.Id, new LockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.None, centerLockResult.Error);
        Assert.Equal(UserStatuses.Locked, centerTarget.Status);

        // Moderator CAN lock Student (subordinate)
        var studentLockResult = await service.LockUserAccountAsync(studentTarget.Id, new LockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.None, studentLockResult.Error);
        Assert.Equal(UserStatuses.Locked, studentTarget.Status);
    }

    [Fact]
    public async Task UnlockUserAccount_ModeratorActor_CanUnlockSubordinatesOnly()
    {
        var service = CreateService();
        var moderatorActor = new User
        {
            Id = Guid.NewGuid(),
            Email = "mod1@test.com",
            Role = UserRoles.Moderator,
            Status = UserStatuses.Active,
            FullName = "Moderator Actor",
            PasswordHash = "x",
        };
        var otherModLocked = new User
        {
            Id = Guid.NewGuid(),
            Email = "mod2@test.com",
            Role = UserRoles.Moderator,
            Status = UserStatuses.Locked,
            FullName = "Other Mod Locked",
            PasswordHash = "x",
        };
        var centerLocked = new User
        {
            Id = Guid.NewGuid(),
            Email = "center@test.com",
            Role = UserRoles.Center,
            Status = UserStatuses.Locked,
            FullName = "Center Locked",
            PasswordHash = "x",
        };
        _repo.Users.AddRange([moderatorActor, otherModLocked, centerLocked]);

        // Moderator cannot unlock another Moderator
        var modUnlockResult = await service.UnlockUserAccountAsync(otherModLocked.Id, new UnlockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.Validation, modUnlockResult.Error);
        Assert.Contains("Kiểm duyệt viên không có quyền", modUnlockResult.Message);

        // Moderator CAN unlock Center
        var centerUnlockResult = await service.UnlockUserAccountAsync(centerLocked.Id, new UnlockUserAccountRequest("reason"), moderatorActor.Id);
        Assert.Equal(AuthError.None, centerUnlockResult.Error);
        Assert.Equal(UserStatuses.Active, centerLocked.Status);
    }

    [Fact]
    public async Task AnonymizeUserAccount_Success_SoftDeletesAndRevokesSessionsAndLogsAudit()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;
        var userId = user.Id;
        var originalEmail = user.Email;
        var originalName = user.FullName;
        var originalHash = user.PasswordHash;

        await _repo.CreateSessionAsync(new UserSession
        {
            UserId = userId,
            TokenHash = "token_hash_abc",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        });

        var callsBefore = _unitOfWork.Calls;   // đăng ký ở trên cũng dùng giao dịch (UC-104): chỉ đếm phần xóa tài khoản
        var result = await service.AnonymizeUserAccountAsync(
            userId,
            ipAddress: "127.0.0.1",
            userAgent: "TestAgent/1.0");

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
        Assert.NotNull(user.DeletedAt);
        // UC-80 xóa mềm: giữ nguyên email, tên, mật khẩu để khôi phục trong 30 ngày
        Assert.Equal(originalEmail, user.Email);
        Assert.Equal(originalName, user.FullName);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.True(_repo.Sessions[0].IsRevoked);
        Assert.Equal(callsBefore + 1, _unitOfWork.Calls);

        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("user.delete_account", log.Action);
        Assert.Equal(userId, log.ActorUserId);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("TestAgent/1.0", log.UserAgent);
    }

    [Fact]
    public async Task AnonymizeUserAccount_WithdrawsOpenLeads_AndLogsCount()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;
        _leadWithdrawals.CountToReturn = 2;

        var result = await service.AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(user.Id, Assert.Single(_leadWithdrawals.UserIds));
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Contains("\"withdrawnLeads\":2", log.DetailJson);
    }

    // [UC-75 quy tắc, UC-130] Phụ huynh xóa tài khoản: hồ sơ con chưa có tài khoản bị xóa mềm cùng lúc, không gửi mail tới con;
    // con thật đã liên kết vẫn còn tài khoản và được báo
    [Fact]
    public async Task AnonymizeUserAccount_Parent_SoftDeletesManagedChildren_KeepsRealChild()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var parent = _repo.Users[0];
        parent.Status = UserStatuses.Active;
        var minh = new User { Email = "minh@noreply.usas.local", FullName = "Minh", Role = UserRoles.Student, Status = UserStatuses.Managed, PasswordHash = "" };
        var realKid = new User { Email = "kid@test.com", FullName = "Kid", Role = UserRoles.Student, Status = UserStatuses.Active, PasswordHash = "x" };
        _repo.Users.AddRange([minh, realKid]);
        _links.Links.Add(new ParentStudentLink { StudentUserId = minh.Id, ParentUserId = parent.Id, ParentEmail = parent.Email, Status = ParentLinkStatuses.Active });
        _links.Links.Add(new ParentStudentLink { StudentUserId = realKid.Id, ParentUserId = parent.Id, ParentEmail = parent.Email, Status = ParentLinkStatuses.Active });
        await _repo.CreateSessionAsync(new UserSession { UserId = minh.Id, TokenHash = "acting", ExpiresAt = DateTime.UtcNow.AddHours(1) });
        var mailsBefore = _emailSender.SentEmails.Count;

        var result = await service.AnonymizeUserAccountAsync(parent.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Deleted, minh.Status);
        Assert.NotNull(minh.DeletedAt);
        Assert.InRange(minh.DeletedAt!.Value - parent.DeletedAt!.Value, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        Assert.True(_repo.Sessions.Single(s => s.UserId == minh.Id).IsRevoked);
        Assert.Equal(UserStatuses.Active, realKid.Status);
        var mails = _emailSender.SentEmails.Skip(mailsBefore).ToList();
        Assert.DoesNotContain(mails, e => e.To == minh.Email);
        Assert.Contains(mails, e => e.To == realKid.Email);
    }

    [Fact]
    public async Task AnonymizeUserAccount_AlreadyDeleted_ReturnsValidationError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Deleted;

        var result = await service.AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("đang chờ xóa", result.Message);
        Assert.Empty(_auditLogs.Entries);
    }

    // [UC-80] Chỉ học sinh và phụ huynh tự xóa được; trung tâm, admin, mod bị từ chối và tài khoản không đổi
    [Theory]
    [InlineData(UserRoles.Center)]
    [InlineData(UserRoles.Admin)]
    [InlineData(UserRoles.Moderator)]
    public async Task AnonymizeUserAccount_RoleNotAllowed_RejectsAndKeepsAccount(string role)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;
        user.Role = role;

        var callsBefore = _unitOfWork.Calls;   // đăng ký ở trên cũng dùng giao dịch (UC-104)
        var result = await service.AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.RoleNotAllowed, result.Error);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Null(user.DeletedAt);
        Assert.Equal(callsBefore, _unitOfWork.Calls);
        Assert.Empty(_auditLogs.Entries);
    }

    [Theory]
    [InlineData(UserRoles.Student)]
    [InlineData(UserRoles.Parent)]
    public async Task AnonymizeUserAccount_StudentOrParent_Allowed(string role)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;
        user.Role = role;

        var result = await service.AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
    }

    // [UC-80] Tạo tài khoản đang chờ xóa, đã xóa mềm cách đây deletedAgo
    private async Task<(AuthService Service, User User)> PendingDeletionSetup(TimeSpan deletedAgo)
    {
        var service = CreateService(new AccountDeletionOptions { RetentionPeriod = TimeSpan.FromDays(30) });
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Deleted;
        user.DeletedAt = DateTime.UtcNow - deletedAgo;
        return (service, user);
    }

    [Fact]
    public async Task Login_PendingDeletion_CorrectPassword_ReturnsRestoreToken_NoSession()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.PendingDeletion, result.Error);
        Assert.Null(result.Response);
        Assert.NotNull(result.PendingDeletion);
        Assert.False(string.IsNullOrEmpty(result.PendingDeletion!.RestoreToken));
        Assert.Equal(user.DeletedAt!.Value.AddDays(30), result.PendingDeletion.DeletionDate);
        Assert.Empty(_repo.Sessions);
        Assert.Equal(UserStatuses.Deleted, user.Status);
        // Mã khôi phục chỉ lưu dạng băm
        var otp = Assert.Single(_repo.Otps, o => o.Purpose == OtpPurposes.RestoreAccount);
        Assert.NotEqual(result.PendingDeletion.RestoreToken, otp.CodeHash);
    }

    [Fact]
    public async Task Login_PendingDeletion_WrongPassword_InvalidCredentials()
    {
        var (service, _) = await PendingDeletionSetup(TimeSpan.FromDays(1));

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Null(result.PendingDeletion);
        Assert.Empty(_repo.Otps.Where(o => o.Purpose == OtpPurposes.RestoreAccount));
    }

    [Fact]
    public async Task Login_PendingDeletion_PastRetention_InvalidCredentials()
    {
        var (service, _) = await PendingDeletionSetup(TimeSpan.FromDays(31));

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Null(result.PendingDeletion);
        Assert.Empty(_repo.Sessions);
    }

    [Fact]
    public async Task RestoreAccount_ValidToken_ActivatesCreatesSessionAndAudits()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        var result = await service.RestoreAccountAsync(
            new RestoreAccountRequest("a@test.com", login.PendingDeletion!.RestoreToken));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Null(user.DeletedAt);
        Assert.Single(_repo.Sessions);
        var log = Assert.Single(_auditLogs.Entries);
        Assert.Equal("user.restore_account", log.Action);
        Assert.Equal(user.Id, log.ActorUserId);
    }

    [Fact]
    public async Task RestoreAccount_WrongToken_Fails()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        var result = await service.RestoreAccountAsync(new RestoreAccountRequest("a@test.com", "sai-ma"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
        Assert.Empty(_repo.Sessions);
    }

    // [UC-05] AUTH-223: tài khoản đang chờ xóa còn trong 30 ngày đặt lại được mật khẩu, tài khoản vẫn chờ xóa;
    // đăng nhập bằng mật khẩu mới đi vào luồng khôi phục (UC-03, 3c)
    [Fact]
    public async Task ResetPassword_PendingDeletion_Succeeds_StaysDeleted_LoginOffersRestore()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var code = _emailSender.SentEmails.Last(e => e.Purpose == OtpPurposes.ResetPassword).Code;
        var reset = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", code, "newPassword123"));

        Assert.Equal(AuthError.None, reset.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "newPassword123"));
        Assert.Equal(AuthError.PendingDeletion, login.Error);
        Assert.False(string.IsNullOrEmpty(login.PendingDeletion!.RestoreToken));
        Assert.Empty(_repo.Sessions);
    }

    // Học sinh dưới 16 bị người đại diện yêu cầu xóa: vẫn đặt lại được mật khẩu nhưng đăng nhập không có mã khôi phục (UC-75 4b)
    [Fact]
    public async Task ResetPassword_PendingDeletionRequestedByParent_Succeeds_LoginStillBlocked()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        user.DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-14));
        user.DeletionRequestedByEmail = "parent@test.com";

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var code = _emailSender.SentEmails.Last(e => e.Purpose == OtpPurposes.ResetPassword).Code;
        var reset = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", code, "newPassword123"));

        Assert.Equal(AuthError.None, reset.Error);
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "newPassword123"));
        Assert.True(login.PendingDeletion!.RequestedByParent);
        Assert.Null(login.PendingDeletion.RestoreToken);
    }

    // Quá 30 ngày (job chưa kịp xóa hẳn) thì không nhận mã, vẫn thông báo chung
    [Fact]
    public async Task ForgotPassword_PendingDeletionPastRetention_NoResetCode()
    {
        var (service, _) = await PendingDeletionSetup(TimeSpan.FromDays(31));

        var forgot = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var resend = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.ResetPassword));

        Assert.Equal(AuthError.None, forgot.Error);
        Assert.Equal(AuthError.None, resend.Error);
        Assert.DoesNotContain(_repo.Otps, o => o.Purpose == OtpPurposes.ResetPassword);
    }

    [Fact]
    public async Task RestoreAccount_TokenUsedTwice_SecondFails()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        var request = new RestoreAccountRequest("a@test.com", login.PendingDeletion!.RestoreToken);
        await service.RestoreAccountAsync(request);
        user.Status = UserStatuses.Deleted;
        user.DeletedAt = DateTime.UtcNow;

        var second = await service.RestoreAccountAsync(request);

        Assert.Equal(AuthError.OtpInvalid, second.Error);
    }

    [Fact]
    public async Task RestoreAccount_ExpiredToken_Fails()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        _repo.Otps.Single(o => o.Purpose == OtpPurposes.RestoreAccount).ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await service.RestoreAccountAsync(
            new RestoreAccountRequest("a@test.com", login.PendingDeletion!.RestoreToken));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
    }

    [Fact]
    public async Task RestoreAccount_PastRetention_Fails()
    {
        var (service, user) = await PendingDeletionSetup(TimeSpan.FromDays(1));
        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        user.DeletedAt = DateTime.UtcNow.AddDays(-31);

        var result = await service.RestoreAccountAsync(
            new RestoreAccountRequest("a@test.com", login.PendingDeletion!.RestoreToken));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(UserStatuses.Deleted, user.Status);
    }

    [Fact]
    public async Task RestoreAccount_ActiveAccount_Fails()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var result = await service.RestoreAccountAsync(new RestoreAccountRequest("a@test.com", "bat-ky"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task AnonymizeUserAccount_UserNotFound_ReturnsValidationError()
    {
        var service = CreateService();

        var result = await service.AnonymizeUserAccountAsync(Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("Không tìm thấy", result.Message);
        Assert.Empty(_auditLogs.Entries);
    }

    // UC-80: liên kết phụ huynh đang hiệu lực của học sinh (kèm consent đã cấp)
    private (User Student, User Parent, ParentStudentLink Link) AddActiveParentLink(int studentAge)
    {
        var student = new User { Email = "kid@test.com", FullName = "Con", Role = UserRoles.Student, Status = UserStatuses.Active, DateOfBirth = BornYearsAgo(studentAge) };
        var parent = new User { Email = "me@test.com", FullName = "Bố", Role = UserRoles.Parent, Status = UserStatuses.Active, DateOfBirth = BornYearsAgo(40) };
        _repo.Users.Add(student);
        _repo.Users.Add(parent);
        var consent = new Consent { UserId = student.Id, Purpose = ConsentPurposes.ChildDataProcessing, Granted = true, GrantedByUserId = parent.Id };
        _links.Consents.Add(consent);
        var link = new ParentStudentLink
        {
            StudentUserId = student.Id,
            ParentEmail = parent.Email,
            ParentUserId = parent.Id,
            Status = ParentLinkStatuses.Active,
            AcceptedAt = DateTime.UtcNow.AddDays(-1),
            ConsentId = consent.Id,
        };
        _links.Links.Add(link);
        return (student, parent, link);
    }

    [Fact]
    public async Task AnonymizeUserAccount_StudentWithActiveLink_RevokesLinkAndConsent_AndNotifiesParent()
    {
        var (student, parent, link) = AddActiveParentLink(studentAge: 17);

        var result = await CreateService().AnonymizeUserAccountAsync(student.Id);

        Assert.Equal(AuthError.None, result.Error);
        var stored = _links.Links.Single(l => l.Id == link.Id);
        Assert.Equal(ParentLinkStatuses.Revoked, stored.Status);
        Assert.Equal(student.Id, stored.RevokedByUserId);
        Assert.False(Assert.Single(_links.Consents).Granted);
        Assert.Contains(_emailSender.SentEmails, e => e.To == parent.Email && e.Purpose == "parent_unlinked");
    }

    [Fact]
    public async Task AnonymizeUserAccount_StudentUnder16_StillRevokesActiveLink()
    {
        // Đặc tả UC-80 cho học sinh dưới 16 tự xóa tài khoản, nên không áp quy tắc chặn của RevokeAsync
        var (student, _, link) = AddActiveParentLink(studentAge: 14);

        var result = await CreateService().AnonymizeUserAccountAsync(student.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(ParentLinkStatuses.Revoked, _links.Links.Single(l => l.Id == link.Id).Status);
    }

    [Fact]
    public async Task AnonymizeUserAccount_StepFailsInsideTransaction_SendsNoEmail()
    {
        var (student, _, _) = AddActiveParentLink(studentAge: 17);
        _auditLogs.ThrowOnAdd = new InvalidOperationException("db down");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().AnonymizeUserAccountAsync(student.Id));

        // Email chỉ gửi sau khi transaction commit; lỗi giữa chừng thì phụ huynh không nhận mail sai
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.Purpose == "parent_unlinked");
    }

    // [UC-80 / billing] Hook gói dịch vụ: gọi trong transaction xóa mềm; việc sau commit (thông báo, hủy link thanh toán) chạy sau khi commit

    private sealed class RecordingBillingHooks(List<string> log) : StudyAbroad.Application.Billing.IBillingAccountHooks
    {
        public Exception? Throw { get; set; }

        public Task<Func<CancellationToken, Task>> OnAccountSoftDeletedAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default)
        {
            if (Throw is not null)
                throw Throw;
            log.Add($"hook:{userId}");
            return Task.FromResult<Func<CancellationToken, Task>>(_ =>
            {
                log.Add("after-commit");
                return Task.CompletedTask;
            });
        }

        public Task<Func<CancellationToken, Task>> OnBeforePurgeAsync(Guid userId, DateTime nowUtc, CancellationToken ct = default) =>
            throw new NotSupportedException();
    }

    private sealed class LoggingUnitOfWork(List<string> log) : IUnitOfWork
    {
        public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
        {
            log.Add("tx-begin");
            await work(ct);
            log.Add("tx-commit");
        }
    }

    private AuthService CreateServiceWithBillingHooks(List<string> log, RecordingBillingHooks hooks) => new(
        _repo, new FakeHasher(), _tokens, _emailSender, _links, _auditLogs,
        new ParentLinkService(_links, _repo, _emailSender, _auditLogs), new LoggingUnitOfWork(log), _leadWithdrawals,
        billingHooks: hooks);

    private async Task<User> ActiveStudent()
    {
        await CreateService().RegisterAsync(Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(18)));
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;
        return user;
    }

    [Fact]
    public async Task AnonymizeUserAccount_CallsBillingHookInsideTransaction_AndItsAfterCommitWorkOnlyAfterCommit()
    {
        var user = await ActiveStudent();
        List<string> log = [];

        var result = await CreateServiceWithBillingHooks(log, new RecordingBillingHooks(log)).AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(new[] { "tx-begin", $"hook:{user.Id}", "tx-commit", "after-commit" }, log);
        Assert.Equal(UserStatuses.Deleted, user.Status);
    }

    [Fact]
    public async Task AnonymizeUserAccount_BillingHookFails_NothingCommittedAndAfterCommitWorkSkipped()
    {
        var user = await ActiveStudent();
        List<string> log = [];
        var hooks = new RecordingBillingHooks(log) { Throw = new InvalidOperationException("db lỗi") };

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateServiceWithBillingHooks(log, hooks).AnonymizeUserAccountAsync(user.Id));

        Assert.Equal(new[] { "tx-begin" }, log);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task AnonymizeUserAccount_RejectedRole_DoesNotCallBillingHook()
    {
        var user = await ActiveStudent();
        user.Role = UserRoles.Admin;
        List<string> log = [];

        var result = await CreateServiceWithBillingHooks(log, new RecordingBillingHooks(log)).AnonymizeUserAccountAsync(user.Id);

        Assert.Equal(AuthError.RoleNotAllowed, result.Error);
        Assert.Empty(log);
    }

    [Fact]
    public async Task CreateCenterAccount_ValidRequest_CreatesActiveCenterUser()
    {
        var service = CreateService();
        var request = new CreateCenterAccountRequest("center@studyabroad.vn", "Password123!", "Trung Tâm Du Học Á Âu", "0912345678");

        var result = await service.CreateCenterAccountAsync(request);

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.User);
        Assert.Equal("center@studyabroad.vn", result.User.Email);
        Assert.Equal(UserRoles.Center, result.User.Role);
        Assert.Equal(UserStatuses.Active, result.User.Status);
        Assert.Equal("Trung Tâm Du Học Á Âu", result.User.FullName);
        Assert.Equal("0912345678", result.User.Phone);

        var savedUser = Assert.Single(_repo.Users);
        Assert.Equal(UserRoles.Center, savedUser.Role);
        Assert.Equal(UserStatuses.Active, savedUser.Status);
    }

    [Fact]
    public async Task CreateCenterAccount_WithoutFullName_DefaultsToEmailPrefix()
    {
        var service = CreateService();
        var request = new CreateCenterAccountRequest("gse.admin@studyabroad.vn", "Password123!", null, "0912345678");

        var result = await service.CreateCenterAccountAsync(request);

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.User);
        Assert.Equal("gse.admin", result.User.FullName);
        Assert.Equal(UserRoles.Center, result.User.Role);
    }

    [Fact]
    public async Task CreateCenterAccount_DuplicateEmail_ReturnsValidationError()
    {
        var service = CreateService();
        var request = new CreateCenterAccountRequest("center@studyabroad.vn", "Password123!", "Trung Tâm A", null);
        await service.CreateCenterAccountAsync(request);

        var duplicateResult = await service.CreateCenterAccountAsync(request);

        Assert.Equal(AuthError.Validation, duplicateResult.Error);
        Assert.Contains("Email đã được sử dụng", duplicateResult.Message);
    }

    [Fact]
    public async Task Login_UnverifiedUser_ReturnsUnverifiedError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.Unverified, result.Error);
    }

    [Fact]
    public async Task Login_WrongPassword_5Times_TriggersTempLockoutAndSendsEmail()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active; // Đã kích hoạt email

        // 4 lần đầu nhập sai
        for (int i = 1; i <= 4; i++)
        {
            var res = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));
            Assert.Equal(AuthError.InvalidCredentials, res.Error);
            Assert.Equal(i, _repo.Users[0].FailedLoginAttempts);
            Assert.Equal(UserStatuses.Active, _repo.Users[0].Status);
        }

        // Lần thứ 5 nhập sai -> Tạm khóa tài khoản nhưng vẫn báo lỗi chung (UC-03 quy tắc)
        var fifthResult = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));
        Assert.Equal(AuthError.InvalidCredentials, fifthResult.Error);
        Assert.Equal(5, _repo.Users[0].FailedLoginAttempts);
        Assert.Equal(UserStatuses.TempLocked, _repo.Users[0].Status);

        // Kiểm tra email cảnh báo đã được gửi (không chứa mã OTP mở khóa)
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "temp_locked_notice");
    }

    [Fact]
    public async Task Login_WhileTempLocked_EvenWithCorrectPassword_FailsWithTempLocked()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.TempLocked;

        // Kể cả nhập ĐÚNG mật khẩu
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.TempLocked, result.Error);
    }

    [Fact]
    public async Task Login_WhileTempLocked_WrongPassword_GetsGenericError_NotCounted()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));

        // [UC-03 quy tắc] Mật khẩu sai không lộ trạng thái khóa
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal(5, user.FailedLoginAttempts);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
    }

    [Fact]
    public async Task Login_TempLockExpired_AutoUnlocksAndLogsIn()
    {
        // [UC-03 E2] Hết 15 phút khóa tạm: đăng nhập đúng mật khẩu được ngay, bộ đếm sai về 0
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1);

        var loginResult = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.None, loginResult.Error);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task Login_TempLockNotExpired_StillBlocked()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);

        var loginResult = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.TempLocked, loginResult.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
    }

    [Fact]
    public async Task Login_AdminLockedUser_PermanentlyBlocked_NeverAutoUnlocks()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Locked; // Admin khóa vĩnh viễn

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.Locked, result.Error);
        Assert.Contains("Quản trị viên", result.Message!);
        Assert.Equal(UserStatuses.Locked, _repo.Users[0].Status);
    }

    [Fact]
    public async Task ForgotPassword_And_ResetPassword_Flow_SucceedsAndRevokesSessions()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        // Đăng nhập tạo session
        await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Single(_repo.Sessions);
        Assert.False(_repo.Sessions[0].IsRevoked);

        // Yêu cầu quên mật khẩu
        var forgotRes = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        Assert.Equal(AuthError.None, forgotRes.Error);

        var resetOtpEmail = _emailSender.SentEmails.Last(e => e.Purpose == OtpPurposes.ResetPassword);

        // Đặt lại mật khẩu mới
        var resetRes = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", resetOtpEmail.Code, "newPassword123"));
        Assert.Equal(AuthError.None, resetRes.Error);

        // Session cũ đã bị thu hồi và chủ tài khoản nhận mail báo đổi mật khẩu
        Assert.True(_repo.Sessions[0].IsRevoked);
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "password_changed_notice" && e.To == "a@test.com");

        // Mật khẩu mới đăng nhập được
        var newLogin = await service.LoginAsync(new LoginRequest("a@test.com", "newPassword123"));
        Assert.Equal(AuthError.None, newLogin.Error);
    }

    [Fact]
    public async Task ResendOtp_Within60Seconds_SilentlyDoesNotSend()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        // Yêu cầu gửi lại ngay lập tức
        var res = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.None, res.Error);
        Assert.Contains("60 giây", res.Message!);
        Assert.Single(_repo.Otps);
        Assert.Single(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Login_LockedAccount_IsLocked()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Locked;

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.Locked, result.Error);
    }

    [Theory]
    [InlineData("6666")]
    [InlineData("+15551234567")]
    [InlineData("0123456789")]
    [InlineData("0243123456")]
    [InlineData("abcdefghij")]
    public async Task Register_InvalidVietnamesePhone_ReturnsValidationError(string invalidPhone)
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request(phone: invalidPhone));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("Số điện thoại không đúng định dạng Việt Nam", result.Message!);
        Assert.Empty(_repo.Users);
    }

    [Theory]
    [InlineData("+84912345678", "0912345678")]
    [InlineData("84912345678", "0912345678")]
    [InlineData("+84 988 123 456", "0988123456")]
    [InlineData("0381234567", "0381234567")]
    [InlineData("079-123-4567", "0791234567")]
    public async Task Register_ValidVietnamesePhoneFormats_NormalizesAndSucceeds(string inputPhone, string expectedPhone)
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request(phone: inputPhone));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(expectedPhone, _repo.Users[0].Phone);
    }

    [Fact]
    public async Task Register_PasswordExceedsMaxLength_ReturnsValidationError()
    {
        var service = CreateService();
        var longPassword = new string('a', 101);
        var result = await service.RegisterAsync(Request(password: longPassword));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("Mật khẩu không được vượt quá", result.Message!);
    }

    // [UC-01 quy tắc mật khẩu] AUTH-020/185: 8 dấu cách bị từ chối, không tạo tài khoản
    [Fact]
    public async Task Register_PasswordOnlyWhitespace_ReturnsValidationError()
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request(password: "        "));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("khoảng trắng", result.Message!);
        Assert.Empty(_repo.Users);
    }

    // [UC-01 quy tắc mật khẩu] AUTH-021: mật khẩu phổ biến bị từ chối (không phân biệt hoa thường)
    [Theory]
    [InlineData("12345678")]
    [InlineData("Password1")]
    [InlineData("QWERTY123")]
    public async Task Register_CommonPassword_ReturnsValidationError(string password)
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request(password: password));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("quá phổ biến", result.Message!);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Login_PayloadExceedsMaxLength_ReturnsInvalidCredentials()
    {
        var service = CreateService();
        var longEmail = new string('a', 101) + "@test.com";
        var result = await service.LoginAsync(new LoginRequest(longEmail, "password123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_Success_ReturnsJwtTokenAndCreatesSession()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var result = await service.LoginAsync(
            new LoginRequest("a@test.com", "password123"),
            ipAddress: "127.0.0.1",
            userAgent: "Mozilla/5.0");

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.NotEmpty(result.Response.AccessToken);
        Assert.Single(_repo.Sessions);
        Assert.False(_repo.Sessions[0].IsRevoked);
        Assert.Equal("127.0.0.1", _repo.Sessions[0].IpAddress);
        Assert.Equal("Mozilla/5.0", _repo.Sessions[0].UserAgent);
    }

    [Fact]
    public async Task Logout_ValidToken_RevokesSession()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var loginResult = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        var rawToken = loginResult.Response!.AccessToken;

        var logoutSuccess = await service.LogoutAsync(rawToken);

        Assert.True(logoutSuccess);
        Assert.Single(_repo.Sessions);
        Assert.True(_repo.Sessions[0].IsRevoked);
    }

    [Fact]
    public async Task GetMe_ExistingUser_ReturnsUserDto()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(phone: "0987654321"));
        var userId = _repo.Users[0].Id;

        var me = await service.GetMeAsync(userId);

        Assert.NotNull(me);
        Assert.Equal(userId, me.Id);
        Assert.Equal("0987654321", me.Phone);
    }

    [Fact]
    public async Task GetMe_ChildStudent_ReturnsAgeRestrictions()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(role: UserRoles.Student, dateOfBirth: BornYearsAgo(14), parentEmail: "me@test.com"));
        var userId = _repo.Users[0].Id;

        var me = await service.GetMeAsync(userId);

        Assert.NotNull(me);
        Assert.Equal(AgeGroups.Child, me.AgeGroup);
        Assert.Equal(ParentConsentStatuses.Pending, me.ParentConsentStatus);
        Assert.Contains(AgeFeatures.ProcessStudentData, me.BlockedFeatures!);
        Assert.Contains(AgeFeatures.ShareWithCenter, me.BlockedFeatures!);
    }

    [Fact]
    public async Task GetAllUsers_ReturnsAllMappedUserDtos()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "user1@test.com", phone: "0911111111"));
        await service.RegisterAsync(Request(email: "user2@test.com", phone: "0922222222"));

        var allUsers = await service.GetAllUsersAsync();

        Assert.Equal(2, allUsers.Count);
        Assert.Contains(allUsers, u => u.Email == "user1@test.com" && u.Phone == "0911111111");
        Assert.Contains(allUsers, u => u.Email == "user2@test.com" && u.Phone == "0922222222");
    }

    [Fact]
    public async Task Register_WithDuplicatePhone_FailsAtPhoneField_NoAccountNoMail()
    {
        // [UC-01 E6] Số điện thoại đã thuộc tài khoản khác: báo lỗi tại ô Số điện thoại
        var service = CreateService();
        await service.RegisterAsync(Request(email: "user1@test.com", phone: "0912345678"));
        var mailsBefore = _emailSender.SentEmails.Count;

        var result = await service.RegisterAsync(Request(email: "user2@test.com", phone: "+84 912 345 678"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(AuthService.PhoneInUseMessage, result.Message);
        Assert.Single(_repo.Users);
        Assert.Equal(mailsBefore, _emailSender.SentEmails.Count);
    }

    [Fact]
    public async Task Register_Generates5MinuteOtp_AndSendsVerificationEmail()
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request());

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
        Assert.Single(_repo.Otps);
        var otp = _repo.Otps[0];
        Assert.Equal(OtpPurposes.VerifyEmail, otp.Purpose);
        // Kiểm tra thời hạn 5 phút
        Assert.True(otp.ExpiresAt > DateTime.UtcNow.AddMinutes(4) && otp.ExpiresAt <= DateTime.UtcNow.AddMinutes(5));
        Assert.Single(_emailSender.SentEmails);
        Assert.Equal("a@test.com", _emailSender.SentEmails[0].To);
    }

    [Fact]
    public async Task VerifyEmail_OtpExpiredAfter5Minutes_ReturnsGenericOtpInvalid()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        // Giả lập OTP đã hết hạn sau 5 phút
        _repo.Otps[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "123456"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(AuthService.OtpFailedMessage, result.Message);
    }

    [Fact]
    public async Task VerifyEmail_WrongOtp5Times_InvalidatesOtp_NeverLocksUserAccount()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];

        // Nhập sai 4 lần
        for (int i = 1; i <= 4; i++)
        {
            var res = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));
            Assert.Equal(AuthError.OtpInvalid, res.Error);
            Assert.Equal(AuthService.OtpFailedMessage, res.Message);
            Assert.Equal(i, _repo.Otps[0].Attempts);
            Assert.Null(_repo.Otps[0].UsedAt);
            Assert.Equal(UserStatuses.Unverified, user.Status);
            Assert.Equal(0, user.FailedLoginAttempts); // Không bao giờ tăng FailedLoginAttempts
        }

        // Nhập sai lần thứ 5 -> Mã OTP bị vô hiệu hóa
        var finalRes = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));
        Assert.Equal(AuthError.OtpInvalid, finalRes.Error);
        Assert.Equal(AuthService.OtpFailedMessage, finalRes.Message);
        Assert.NotNull(_repo.Otps[0].UsedAt); // Đã bị hủy
        Assert.Equal(UserStatuses.Unverified, user.Status); // Tài khoản vẫn unverified, không bị khóa
    }

    [Fact]
    public async Task VerifyEmail_CorrectOtp_MarksUsedAndActivatesUser()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var code = _emailSender.SentEmails[0].Code;

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", code));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(UserStatuses.Active, _repo.Users[0].Status);
        Assert.NotNull(_repo.Otps[0].UsedAt); // Mã đã dùng thành công và bị hủy
    }

    [Fact]
    public async Task ResendOtp_UnknownEmail_ReturnsSameMessageAsExistingEmail()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Otps[0].CreatedAt = DateTime.UtcNow.AddSeconds(-61);

        var known = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));
        var unknown = await service.ResendOtpAsync(new ResendOtpRequest("nobody@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.None, unknown.Error);
        Assert.Equal(known.Message, unknown.Message);
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.To == "nobody@test.com");
    }

    [Fact]
    public async Task ResendOtp_MoreThan5TimesPerHour_SilentlyDoesNotSend()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];

        // [UC-02] Mã lúc đăng ký + 5 lần gửi lại trong 1 giờ qua: lần gửi lại thứ 6 không gửi
        for (int i = 0; i < 5; i++)
        {
            _repo.Otps.Add(new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = "hash",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            });
        }
        // Cho lần gần nhất quá 60s
        _repo.Otps[0].CreatedAt = DateTime.UtcNow.AddMinutes(-5);

        var result = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(6, _repo.Otps.Count); // không sinh thêm mã
        Assert.Single(_emailSender.SentEmails);
    }

    [Fact]
    public async Task ResendOtp_FifthResendInHour_StillSends_RegistrationCodeNotCounted()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        _repo.Otps[0].CreatedAt = DateTime.UtcNow.AddMinutes(-30);

        // [UC-02] Đã gửi lại 4 lần (mã lúc đăng ký không tính): lần gửi lại thứ 5 vẫn được gửi
        for (int i = 0; i < 4; i++)
        {
            _repo.Otps.Add(new OtpToken
            {
                UserId = user.Id,
                Purpose = OtpPurposes.VerifyEmail,
                CodeHash = "hash",
                CreatedAt = DateTime.UtcNow.AddMinutes(-20 + i),
                ExpiresAt = DateTime.UtcNow.AddMinutes(5)
            });
        }

        var result = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(6, _repo.Otps.Count);
        Assert.Equal(2, _emailSender.SentEmails.Count);
    }

    [Fact]
    public async Task Login_WrongPassword5Times_TempLocks15Minutes()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;

        // Nhập sai 4 lần
        for (int i = 1; i <= 4; i++)
        {
            var res = await service.LoginAsync(new LoginRequest("a@test.com", "wrongpass"));
            Assert.Equal(AuthError.InvalidCredentials, res.Error);
            Assert.Equal(i, user.FailedLoginAttempts);
            Assert.Equal(UserStatuses.Active, user.Status);
        }

        // Nhập sai lần thứ 5 -> Khóa tài khoản
        var lockRes = await service.LoginAsync(new LoginRequest("a@test.com", "wrongpass"));
        Assert.Equal(AuthError.InvalidCredentials, lockRes.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
        Assert.NotNull(user.LockoutEnd);
        Assert.InRange(user.LockoutEnd!.Value, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(15));

        // Kiểm tra email thông báo đã được gửi (không chứa mã OTP mở khóa)
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "temp_locked_notice");
    }

    [Fact]
    public async Task Login_TempLockedUser_IsBlocked_EvenWithCorrectPassword()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;

        // Kể cả gõ đúng mật khẩu vẫn bị chặn
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.TempLocked, result.Error);
        Assert.Contains("15 phút", result.Message!);
    }

    [Fact]
    public async Task Login_TempLockedUser_AdminCanUnlockEarly()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10);

        // Còn trong 15 phút: đăng nhập vẫn bị chặn
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Equal(AuthError.TempLocked, result.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);

        // Admin mở khóa sớm thì đăng nhập được ngay
        var adminId = Guid.NewGuid();
        var unlockResult = await service.UnlockUserAccountAsync(user.Id, new UnlockUserAccountRequest("Admin mở khóa"), adminId);
        Assert.Equal(AuthError.None, unlockResult.Error);
        Assert.Equal(UserStatuses.Active, user.Status);

        var loginAfterAdminUnlock = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Equal(AuthError.None, loginAfterAdminUnlock.Error);
        Assert.NotNull(loginAfterAdminUnlock.Response);
    }

    [Fact]
    public async Task BackgroundJob_UnlockExpiredAccounts_AutoUnlocksExpiredTempLockedUsers()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "locked@test.com",
            Role = UserRoles.Student,
            Status = UserStatuses.TempLocked,
            FailedLoginAttempts = 5,
            LockoutEnd = DateTime.UtcNow.AddMinutes(-5) // Đã quá 15 phút
        };
        _repo.Users.Add(user);

        var count = await _repo.UnlockExpiredAccountsAsync();

        Assert.Equal(1, count);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
    }

    [Fact]
    public async Task BackgroundJob_CleanupExpiredOtps_RemovesExpiredTokens()
    {
        _repo.Otps.Add(new OtpToken
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10) // Đã hết hạn
        });
        _repo.Otps.Add(new OtpToken
        {
            Id = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(4) // Còn hạn
        });

        var deleted = await _repo.CleanupExpiredOtpsAsync();

        Assert.Equal(1, deleted);
        Assert.Single(_repo.Otps);
        Assert.True(_repo.Otps[0].ExpiresAt > DateTime.UtcNow);
    }

    // =========================================================================
    // CÁC TEST TRƯỜNG HỢP BIÊN (BOUNDARY VALUE ANALYSIS & EQUIVALENCE PARTITIONING)
    // =========================================================================

    [Fact]
    public async Task Login_WrongPassword4Times_ThenCorrectOn5th_ResetsFailedAttemptsAndSucceeds()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;

        // Sai liên tiếp 4 lần (ngay sát biên 5 lần)
        for (int i = 1; i <= 4; i++)
        {
            var fail = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-pass"));
            Assert.Equal(AuthError.InvalidCredentials, fail.Error);
            Assert.Equal(i, user.FailedLoginAttempts);
            Assert.Equal(UserStatuses.Active, user.Status);
        }

        // Lần thứ 5 gõ ĐÚNG mật khẩu -> Thành công và reset về 0
        var success = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Equal(AuthError.None, success.Error);
        Assert.NotNull(success.Response);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
        Assert.Equal(UserStatuses.Active, user.Status);
    }

    [Fact]
    public async Task VerifyEmail_WrongOtp4Times_FifthAttemptCorrect_SucceedsAndActivates()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var correctCode = _emailSender.SentEmails[0].Code;
        var user = _repo.Users[0];

        // Sai 4 lần liên tiếp
        for (int i = 1; i <= 4; i++)
        {
            var res = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "999999"));
            Assert.Equal(AuthError.OtpInvalid, res.Error);
            Assert.Equal(i, _repo.Otps[0].Attempts);
        }

        // Lần thứ 5 nhập ĐÚNG -> Thành công, không bị hủy
        var finalRes = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", correctCode));
        Assert.Equal(AuthError.None, finalRes.Error);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.NotNull(_repo.Otps[0].UsedAt);
    }

    [Fact]
    public async Task VerifyEmail_AfterOtpInvalidatedBy5WrongAttempts_CorrectCodeIsStillRejected()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var correctCode = _emailSender.SentEmails[0].Code;

        // Gõ sai 4 lần đầu
        for (int i = 1; i <= 4; i++)
        {
            var res = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));
            Assert.Equal(AuthError.OtpInvalid, res.Error);
        }

        // Lần thứ 5 sai -> Bị vô hiệu hóa ngay
        var attempt5 = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));
        Assert.Equal(AuthError.OtpInvalid, attempt5.Error);
        Assert.NotNull(_repo.Otps[0].UsedAt); // Đã bị hủy

        // Lần thứ 6 gõ ĐÚNG mã cũ -> Vẫn bị từ chối vì mã OTP đó đã không còn hợp lệ
        var attempt6 = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", correctCode));
        Assert.Equal(AuthError.OtpInvalid, attempt6.Error);
        Assert.Equal(AuthService.OtpFailedMessage, attempt6.Message);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
    }

    [Fact]
    public async Task ResendOtp_After61Seconds_Succeeds()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        // Giả lập lần gửi trước cách đây 61 giây (vượt qua biên 60s)
        _repo.Otps[0].CreatedAt = DateTime.UtcNow.AddSeconds(-61);

        var result = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(2, _repo.Otps.Count); // Có thêm mã OTP mới
        Assert.Equal(2, _emailSender.SentEmails.Count);
    }

    [Fact]
    public async Task ResendOtp_AdminLockedUser_SilentlyDoesNotSend()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Locked; // Bị Admin khóa

        var result = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.ResetPassword));

        Assert.Equal(AuthError.None, result.Error);
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.Purpose == OtpPurposes.ResetPassword);
    }

    [Fact]
    public async Task Login_UnverifiedAccount_ReturnsUnverifiedError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        // Tài khoản vừa đăng ký có status unverified

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.Unverified, result.Error);
        Assert.Contains("chưa được kích hoạt", result.Message!);
    }

    [Fact]
    public async Task ResetPassword_WrongOtp5Times_InvalidatesOtp_PasswordIsNotChanged()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;
        var oldHash = _repo.Users[0].PasswordHash;

        await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var resetOtp = _repo.Otps.Last(o => o.Purpose == OtpPurposes.ResetPassword);

        // Gõ sai OTP 5 lần
        for (int i = 1; i <= 5; i++)
        {
            var fail = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", "000000", "newpassword123"));
            Assert.Equal(AuthError.OtpInvalid, fail.Error);
        }

        Assert.NotNull(resetOtp.UsedAt); // Đã bị hủy
        Assert.Equal(oldHash, _repo.Users[0].PasswordHash); // Mật khẩu cũ không đổi
    }

    [Fact]
    public async Task BackgroundJob_UnlockExpiredAccounts_OnlyUnlocksExpired_IgnoresActiveLockouts()
    {
        var expiredUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "expired@test.com",
            Role = UserRoles.Student,
            Status = UserStatuses.TempLocked,
            FailedLoginAttempts = 5,
            LockoutEnd = DateTime.UtcNow.AddMinutes(-2) // Đã quá 15 phút
        };
        var activeLockoutUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "active_lock@test.com",
            Role = UserRoles.Student,
            Status = UserStatuses.TempLocked,
            FailedLoginAttempts = 5,
            LockoutEnd = DateTime.UtcNow.AddMinutes(10) // Còn 10 phút nữa
        };
        _repo.Users.Add(expiredUser);
        _repo.Users.Add(activeLockoutUser);

        var count = await _repo.UnlockExpiredAccountsAsync();

        Assert.Equal(1, count);
        Assert.Equal(UserStatuses.Active, expiredUser.Status);
        Assert.Equal(0, expiredUser.FailedLoginAttempts);
        Assert.Null(expiredUser.LockoutEnd);

        // User chưa hết 15 phút vẫn phải giữ nguyên
        Assert.Equal(UserStatuses.TempLocked, activeLockoutUser.Status);
        Assert.Equal(5, activeLockoutUser.FailedLoginAttempts);
        Assert.NotNull(activeLockoutUser.LockoutEnd);
    }

    // =========================================================================
    // CHỐNG DÒ TÀI KHOẢN & REQUEST SONG SONG
    // =========================================================================

    [Fact]
    public async Task Login_UnknownEmail_SameResultAsWrongPassword()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var unknown = await service.LoginAsync(new LoginRequest("nobody@test.com", "password123"));
        var wrong = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));

        Assert.Equal(AuthError.InvalidCredentials, unknown.Error);
        Assert.Equal(wrong.Error, unknown.Error);
        Assert.Equal(wrong.Message, unknown.Message);
    }

    [Theory]
    [InlineData(UserStatuses.Unverified)]
    [InlineData(UserStatuses.Locked)]
    public async Task Login_UnverifiedOrLocked_WrongPassword_DoesNotRevealStatusOrCountAttempts(string status)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = status;

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal(AuthService.InvalidCredentialsMessage, result.Message);
        Assert.Equal(0, _repo.Users[0].FailedLoginAttempts);
        Assert.Equal(status, _repo.Users[0].Status);
    }

    [Fact]
    public async Task Login_ConcurrentAttemptConflict_RejectedBeforeCheckingPassword()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;
        _repo.UserConflictsToThrow = 1; // request khác vừa ghi bộ đếm

        // Kể cả mật khẩu đúng: không ghi được lần thử thì không được so mật khẩu
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Empty(_repo.Sessions);
    }

    [Fact]
    public async Task VerifyEmail_ConcurrentAttemptConflict_RejectedWithoutConsumingOtp()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var code = _emailSender.SentEmails[0].Code;
        _repo.OtpConflictsToThrow = 1;

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", code));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Null(_repo.Otps[0].UsedAt);
        Assert.Equal(UserStatuses.Unverified, _repo.Users[0].Status);
        Assert.Empty(_repo.Sessions);
    }

    [Fact]
    public async Task VerifyEmail_SameCodeUsedTwice_SecondIsRejected()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var code = _emailSender.SentEmails[0].Code;

        var first = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", code));
        var second = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", code));

        Assert.Equal(AuthError.None, first.Error);
        Assert.Equal(AuthError.OtpInvalid, second.Error);
        Assert.Single(_repo.Sessions);
    }

    [Fact]
    public async Task VerifyEmail_UnknownEmail_SameMessageAsWrongCode()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var unknown = await service.VerifyEmailAsync(new VerifyEmailRequest("nobody@test.com", "000000"));
        var wrong = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));

        Assert.Equal(AuthError.OtpInvalid, unknown.Error);
        Assert.Equal(wrong.Message, unknown.Message);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_SameMessageAndNoMail()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;

        var known = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var unknown = await service.ForgotPasswordAsync(new ForgotPasswordRequest("nobody@test.com"));

        Assert.Equal(known.Message, unknown.Message);
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.To == "nobody@test.com");
    }

    // AUTH-126: tài khoản chưa kích hoạt hoặc bị khóa xin đặt lại mật khẩu thì nhận cùng thông báo chung nhưng không có mã
    [Theory]
    [InlineData(UserStatuses.Unverified)]
    [InlineData(UserStatuses.Locked)]
    public async Task ForgotPassword_UnverifiedOrLocked_SameMessage_NoResetCode(string status)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = status;
        var unknown = await service.ForgotPasswordAsync(new ForgotPasswordRequest("nobody@test.com"));

        var forgot = await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var resend = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.ResetPassword));

        Assert.Equal(AuthError.None, forgot.Error);
        Assert.Equal(unknown.Message, forgot.Message);
        Assert.Equal(AuthError.None, resend.Error);
        Assert.DoesNotContain(_repo.Otps, o => o.Purpose == OtpPurposes.ResetPassword);
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.Purpose == OtpPurposes.ResetPassword);
    }

    [Theory]
    [InlineData("short")]
    [InlineData(null)]
    public async Task ResetPassword_InvalidNewPassword_IsValidationErrorAndOtpUntouched(string? newPassword)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;
        await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var resetOtp = _repo.Otps.Last(o => o.Purpose == OtpPurposes.ResetPassword);
        var code = _emailSender.SentEmails.Last(e => e.Purpose == OtpPurposes.ResetPassword).Code;

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", code, newPassword!));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(0, resetOtp.Attempts);
        Assert.Null(resetOtp.UsedAt);
    }

    [Fact]
    public async Task ResetPassword_AdminLockedUser_RejectedWithGenericOtpMessage()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Active;
        await service.ForgotPasswordAsync(new ForgotPasswordRequest("a@test.com"));
        var code = _emailSender.SentEmails.Last(e => e.Purpose == OtpPurposes.ResetPassword).Code;
        var oldHash = _repo.Users[0].PasswordHash;
        _repo.Users[0].Status = UserStatuses.Locked;

        var result = await service.ResetPasswordAsync(new ResetPasswordRequest("a@test.com", code, "newPassword123"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(AuthService.OtpFailedMessage, result.Message);
        Assert.Equal(oldHash, _repo.Users[0].PasswordHash);
    }

    // [UC-06] Đổi mật khẩu: tạo tài khoản active và 2 phiên (thiết bị hiện tại + thiết bị khác)
    private async Task<(AuthService Service, User User, string CurrentToken)> ActiveUserWithTwoSessionsAsync()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.Active;

        var login = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        var currentToken = login.Response!.AccessToken;
        await _repo.CreateSessionAsync(new UserSession { UserId = user.Id, TokenHash = "hash:other-device", ExpiresAt = DateTime.UtcNow.AddDays(7) });
        return (service, user, currentToken);
    }

    [Fact]
    public async Task ChangePassword_Success_KeepsCurrentSession_RevokesOthers_AndMailsOwner()
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();

        var result = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("password123", "newPassword123"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal("hashed:newPassword123", user.PasswordHash);
        Assert.False(_repo.Sessions.Single(s => s.TokenHash == "hash:" + currentToken).IsRevoked);
        Assert.True(_repo.Sessions.Single(s => s.TokenHash == "hash:other-device").IsRevoked);
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "password_changed_notice" && e.To == "a@test.com");
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_RejectedAndCounted_NoChange()
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();

        var result = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("wrongpass", "newPassword123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal(AuthService.WrongCurrentPasswordMessage, result.Message);
        Assert.Equal("hashed:password123", user.PasswordHash);
        Assert.Equal(1, user.FailedLoginAttempts);
        Assert.All(_repo.Sessions, s => Assert.False(s.IsRevoked));
        Assert.DoesNotContain(_emailSender.SentEmails, e => e.Purpose == "password_changed_notice");
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword5Times_TempLocksAndRevokesAllSessions()
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();

        for (int i = 1; i <= 4; i++)
        {
            var res = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("wrongpass", "newPassword123"));
            Assert.Equal(AuthError.InvalidCredentials, res.Error);
        }

        var lockRes = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("wrongpass", "newPassword123"));

        Assert.Equal(AuthError.TempLocked, lockRes.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
        Assert.NotNull(user.LockoutEnd);
        Assert.InRange(user.LockoutEnd!.Value, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(15));
        Assert.All(_repo.Sessions, s => Assert.True(s.IsRevoked));
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "temp_locked_notice");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("")]
    public async Task ChangePassword_InvalidNewPassword_IsValidationError_NotCounted(string newPassword)
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();

        var result = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("password123", newPassword));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal("hashed:password123", user.PasswordHash);
        Assert.Equal(0, user.FailedLoginAttempts);
    }

    [Fact]
    public async Task ChangePassword_NewSameAsCurrent_IsValidationError_SessionsUntouched()
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();

        var result = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("password123", "password123"));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.All(_repo.Sessions, s => Assert.False(s.IsRevoked));
    }

    [Fact]
    public async Task ChangePassword_ConcurrentAttemptConflict_RejectedBeforeCheckingPassword()
    {
        var (service, user, currentToken) = await ActiveUserWithTwoSessionsAsync();
        _repo.UserConflictsToThrow = 1;

        var result = await service.ChangePasswordAsync(user.Id, currentToken, new ChangePasswordRequest("password123", "newPassword123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal("hashed:password123", user.PasswordHash);
        Assert.All(_repo.Sessions, s => Assert.False(s.IsRevoked));
    }

    // Sửa thông tin cá nhân: tên + số điện thoại, email giữ nguyên
    private async Task<(AuthService Service, User User)> ActiveUserAsync(string email = "a@test.com", string? phone = null)
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: email, phone: phone));
        var user = _repo.Users.Single(u => u.Email == email);
        user.Status = UserStatuses.Active;
        return (service, user);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNameAndNormalizesPhone_EmailUnchanged()
    {
        var (service, user) = await ActiveUserAsync();

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("  Tran Thi B  ", "+84 912 345 678"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal("Tran Thi B", user.FullName);
        Assert.Equal("0912345678", user.Phone);
        Assert.Equal("a@test.com", user.Email);
        Assert.Equal("Tran Thi B", result.User!.FullName);
    }

    [Fact]
    public async Task UpdateProfile_EmptyPhone_ClearsPhone()
    {
        var (service, user) = await ActiveUserAsync(phone: "0912345678");

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Nguyen Van A", "  "));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Null(user.Phone);
    }

    [Theory]
    [InlineData("", "0912345678")]
    [InlineData("Nguyen Van A", "12345")]
    public async Task UpdateProfile_InvalidInput_Rejected_NoChange(string fullName, string phone)
    {
        var (service, user) = await ActiveUserAsync();

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest(fullName, phone));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal("Nguyen Van A", user.FullName);
        Assert.Null(user.Phone);
    }

    [Fact]
    public async Task UpdateProfile_PhoneOfAnotherAccount_Rejected_OwnPhoneAllowed()
    {
        await ActiveUserAsync(email: "other@test.com", phone: "0987654321");
        var (service, user) = await ActiveUserAsync(phone: "0912345678");

        var taken = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Nguyen Van A", "0987654321"));
        var own = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Nguyen Van C", "0912345678"));

        Assert.Equal(AuthError.Validation, taken.Error);
        Assert.Equal(AuthError.None, own.Error);
        Assert.Equal("0912345678", user.Phone);
        Assert.Equal("Nguyen Van C", user.FullName);
    }

    // [UC-111] Chỉ sửa tên và số điện thoại; vai trò, ngày sinh, email, trạng thái giữ nguyên
    [Fact]
    public async Task UpdateProfile_NeverChangesRoleDateOfBirthEmailOrStatus()
    {
        var (service, user) = await ActiveUserAsync();
        user.DateOfBirth = new DateOnly(2000, 5, 20);
        var role = user.Role;

        var result = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Tên mới", "0912345678"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(role, user.Role);
        Assert.Equal(new DateOnly(2000, 5, 20), user.DateOfBirth);
        Assert.Equal("a@test.com", user.Email);
        Assert.Equal(UserStatuses.Active, user.Status);
        // Người dùng xem được ngày sinh của chính mình
        Assert.Equal(new DateOnly(2000, 5, 20), result.User!.DateOfBirth);
        Assert.Equal(role, result.User.Role);
    }

    [Fact]
    public async Task GetMe_ReturnsOwnDateOfBirth()
    {
        var (service, user) = await ActiveUserAsync();
        user.DateOfBirth = new DateOnly(2001, 1, 2);

        var me = await service.GetMeAsync(user.Id);

        Assert.Equal(new DateOnly(2001, 1, 2), me!.DateOfBirth);
    }

    [Fact]
    public async Task UpdateProfile_CenterCannotChangePhoneHere_ButCanChangeNameAndKeepPhone()
    {
        var (service, user) = await ActiveUserAsync(phone: "0912345678");
        user.Role = UserRoles.Center;

        var changePhone = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Tên mới", "0987654321"));
        var clearPhone = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Tên mới", null));
        var sameName = await service.UpdateProfileAsync(user.Id, new UpdateProfileRequest("Trung tâm ABC", "0912345678"));

        Assert.Equal(AuthError.Validation, changePhone.Error);
        Assert.Equal(AuthError.Validation, clearPhone.Error);
        Assert.Equal(AuthError.None, sameName.Error);
        Assert.Equal("0912345678", user.Phone);
        Assert.Equal("Trung tâm ABC", user.FullName);
    }

    // ---- [UC-113] Admin sửa ngày sinh theo yêu cầu hỗ trợ ----

    private async Task<Guid> RegisterActive(string role, int age, string email = "u@test.com", string? parentEmail = null)
    {
        await CreateService().RegisterAsync(Request(email: email, role: role, dateOfBirth: BornYearsAgo(age), parentEmail: parentEmail));
        var user = _repo.Users.Single(u => u.Email == email);
        user.Status = UserStatuses.Active;
        return user.Id;
    }

    [Fact]
    public async Task UpdateBirthDate_Student_UpdatesDobRecomputesAgeAndAuditsOldAndNew()
    {
        var adminId = Guid.NewGuid();
        var id = await RegisterActive(UserRoles.Student, 15, parentEmail: "p@test.com");
        var oldDob = _repo.Users.Single(u => u.Id == id).DateOfBirth;
        var newDob = BornYearsAgo(19);

        var result = await CreateService().UpdateUserBirthDateAsync(id, new UpdateUserBirthDateRequest(newDob, "  Khai nhầm năm sinh  "), adminId);

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(newDob, _repo.Users.Single(u => u.Id == id).DateOfBirth);
        Assert.Equal(19, result.User!.Age);
        Assert.Equal(AgeGroups.Adult, result.User.AgeGroup);
        Assert.Equal(ParentConsentStatuses.NotRequired, result.User.ParentConsentStatus);
        var log = Assert.Single(_auditLogs.Entries, e => e.Action == "user.birth_date_update");
        Assert.Equal(adminId, log.ActorUserId);
        using var detail = System.Text.Json.JsonDocument.Parse(log.DetailJson!);
        Assert.Equal(oldDob!.Value.ToString("yyyy-MM-dd"), detail.RootElement.GetProperty("oldBirthDate").GetString());
        Assert.Equal(newDob.ToString("yyyy-MM-dd"), detail.RootElement.GetProperty("newBirthDate").GetString());
        Assert.Equal("Khai nhầm năm sinh", detail.RootElement.GetProperty("reason").GetString());
        Assert.Equal(15, detail.RootElement.GetProperty("oldAge").GetInt32());
        Assert.Equal(19, detail.RootElement.GetProperty("newAge").GetInt32());
    }

    [Fact]
    public async Task UpdateBirthDate_AdultStudentBecomesChild_RequiresParentConsentAgain()
    {
        var id = await RegisterActive(UserRoles.Student, 20);

        var result = await CreateService().UpdateUserBirthDateAsync(id, new UpdateUserBirthDateRequest(BornYearsAgo(14), "Khai sai"), Guid.NewGuid());

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(AgeGroups.Child, result.User!.AgeGroup);
        Assert.Equal(ParentConsentStatuses.None, result.User.ParentConsentStatus);
        Assert.NotEmpty(result.User.BlockedFeatures!);
    }

    [Fact]
    public async Task UpdateBirthDate_ParentMustStayAtLeast18()
    {
        var id = await RegisterActive(UserRoles.Parent, 40);

        var tooYoung = await CreateService().UpdateUserBirthDateAsync(id, new UpdateUserBirthDateRequest(BornYearsAgo(17), "x"), Guid.NewGuid());
        var ok = await CreateService().UpdateUserBirthDateAsync(id, new UpdateUserBirthDateRequest(BornYearsAgo(25), "x"), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, tooYoung.Error);
        Assert.Equal(AuthError.None, ok.Error);
        Assert.Equal(BornYearsAgo(25), _repo.Users.Single(u => u.Id == id).DateOfBirth);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task UpdateBirthDate_EmptyReason_IsRejectedAndNothingChanges(string? reason)
    {
        var id = await RegisterActive(UserRoles.Student, 20);
        var before = _repo.Users.Single(u => u.Id == id).DateOfBirth;

        var result = await CreateService().UpdateUserBirthDateAsync(id, new UpdateUserBirthDateRequest(BornYearsAgo(19), reason), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal(before, _repo.Users.Single(u => u.Id == id).DateOfBirth);
        Assert.DoesNotContain(_auditLogs.Entries, e => e.Action == "user.birth_date_update");
    }

    [Fact]
    public async Task UpdateBirthDate_InvalidInputs_AreRejected()
    {
        var id = await RegisterActive(UserRoles.Student, 20);
        var current = _repo.Users.Single(u => u.Id == id).DateOfBirth;
        var today = AgePolicy.TodayInVietnam(DateTime.UtcNow);
        var service = CreateService();
        var admin = Guid.NewGuid();
        var tooLong = new string('a', AuthService.MaxLockReasonLength + 1);

        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(id, new(today.AddDays(1), "x"), admin)).Error);
        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(id, new(BornYearsAgo(AuthService.MaxAge + 1), "x"), admin)).Error);
        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(id, new(null, "x"), admin)).Error);
        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(id, new(BornYearsAgo(19), tooLong), admin)).Error);
        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(id, new(current, "x"), admin)).Error);
        Assert.Equal(AuthError.Validation, (await service.UpdateUserBirthDateAsync(Guid.NewGuid(), new(BornYearsAgo(19), "x"), admin)).Error);
        Assert.Equal(current, _repo.Users.Single(u => u.Id == id).DateOfBirth);
    }

    [Theory]
    [InlineData(UserRoles.Center)]
    [InlineData(UserRoles.Moderator)]
    [InlineData(UserRoles.Admin)]
    public async Task UpdateBirthDate_OnlyStudentOrParentAccounts(string role)
    {
        var id = await RegisterActive(UserRoles.Parent, 30);
        _repo.Users.Single(u => u.Id == id).Role = role;

        var result = await CreateService().UpdateUserBirthDateAsync(id, new(BornYearsAgo(31), "x"), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
    }

    // [1.5c] Con managed: phụ huynh nhập thay, không mật khẩu, không tự đăng nhập hay đặt lại mật khẩu
    private User AddManagedChild()
    {
        var child = new User
        {
            Email = "managed-abc@noreply.usas.local",
            PasswordHash = string.Empty,
            FullName = "Con Managed",
            Role = UserRoles.Student,
            Status = UserStatuses.Managed,
        };
        _repo.Users.Add(child);
        return child;
    }

    [Fact]
    public async Task Login_ManagedChild_InvalidCredentials_AndStaysManagedAfterManyTries()
    {
        var child = AddManagedChild();
        var service = CreateService();

        AuthResult result = null!;
        for (var i = 0; i < 6; i++)
            result = await service.LoginAsync(new LoginRequest(child.Email, "password123"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal(UserStatuses.Managed, child.Status);
        Assert.Equal(0, child.FailedLoginAttempts);
        Assert.Empty(_repo.Sessions);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task ForgotPassword_ManagedChild_SameMessage_NoOtpNoEmail()
    {
        var child = AddManagedChild();

        var result = await CreateService().ForgotPasswordAsync(new ForgotPasswordRequest(child.Email));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Empty(_repo.Otps);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task ResendOtp_ManagedChild_SameMessage_NoOtpNoEmail()
    {
        var child = AddManagedChild();

        var result = await CreateService().ResendOtpAsync(new ResendOtpRequest(child.Email, OtpPurposes.ResetPassword));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Empty(_repo.Otps);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task ResetPassword_ManagedChild_OtpInvalid_PasswordUnchanged()
    {
        var child = AddManagedChild();

        var result = await CreateService().ResetPasswordAsync(new ResetPasswordRequest(child.Email, "123456", "NewPassword1!"));

        Assert.Equal(AuthError.OtpInvalid, result.Error);
        Assert.Equal(string.Empty, child.PasswordHash);
    }

    [Fact]
    public async Task ChangePassword_ManagedChild_Rejected_PasswordUnchanged()
    {
        var child = AddManagedChild();

        var result = await CreateService().ChangePasswordAsync(child.Id, "token", new ChangePasswordRequest("x", "NewPassword1!"));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Equal(string.Empty, child.PasswordHash);
        Assert.Equal(0, child.FailedLoginAttempts);
    }

    [Fact]
    public async Task LockUserAccount_ManagedChild_NotFound_StaysManaged()
    {
        var child = AddManagedChild();

        var result = await CreateService().LockUserAccountAsync(child.Id, new LockUserAccountRequest("lý do"), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal("Không tìm thấy tài khoản.", result.Message);
        Assert.Equal(UserStatuses.Managed, child.Status);
        Assert.Empty(_auditLogs.Entries);
        Assert.Empty(_emailSender.SentEmails);
    }

    [Fact]
    public async Task UnlockUserAccount_ManagedChild_NotFound_StaysManaged()
    {
        var child = AddManagedChild();

        var result = await CreateService().UnlockUserAccountAsync(child.Id, new UnlockUserAccountRequest("lý do"), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal("Không tìm thấy tài khoản.", result.Message);
        Assert.Equal(UserStatuses.Managed, child.Status);
        Assert.Empty(_auditLogs.Entries);
    }

    [Fact]
    public async Task UpdateUserBirthDate_ManagedChild_NotFound_BirthDateUnchanged()
    {
        var child = AddManagedChild();

        var result = await CreateService().UpdateUserBirthDateAsync(child.Id, new UpdateUserBirthDateRequest(BornYearsAgo(14), "Khai sai"), Guid.NewGuid());

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Equal("Không tìm thấy tài khoản.", result.Message);
        Assert.Null(child.DateOfBirth);
        Assert.Empty(_auditLogs.Entries);
    }
}
