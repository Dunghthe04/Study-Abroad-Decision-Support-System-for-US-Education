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

    private AuthService CreateService() => new(_repo, new FakeHasher(), _tokens, _emailSender, _links, _auditLogs);

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

        // Lần thứ 5 nhập sai -> Tạm khóa tài khoản
        var fifthResult = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));
        Assert.Equal(AuthError.TempLocked, fifthResult.Error);
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
        _repo.Users[0].LockoutEnd = DateTime.UtcNow.AddMinutes(10);

        // Kể cả nhập ĐÚNG mật khẩu
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.TempLocked, result.Error);
    }

    [Fact]
    public async Task Login_TempLockedUser_After15Minutes_AutoUnlocksAtRuntimeAndSucceeds()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // Đã qua 15 phút

        var loginResult = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.None, loginResult.Error);
        Assert.NotNull(loginResult.Response);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
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
    public async Task Register_WithDuplicatePhone_ReturnsGenericSuccessAndMailsNewEmail()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "user1@test.com", phone: "0912345678"));

        var result = await service.RegisterAsync(Request(email: "user2@test.com", phone: "+84 912 345 678"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Contains("user2@test.com", result.Message!);
        Assert.Single(_repo.Users);
        Assert.Equal(("user2@test.com", "phone_in_use_notice"), (_emailSender.SentEmails[^1].To, _emailSender.SentEmails[^1].Purpose));
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

        // Giả lập đã có 5 OTP tokens được tạo trong 1 giờ qua
        for (int i = 0; i < 4; i++)
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
        Assert.Equal(5, _repo.Otps.Count); // không sinh thêm mã
        Assert.Single(_emailSender.SentEmails);
    }

    [Fact]
    public async Task Login_WrongPassword5Times_TempLocksUserFor15Minutes_AndSendsNoticeEmail()
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

        // Nhập sai lần thứ 5 -> Tạm khóa 15 phút
        var lockRes = await service.LoginAsync(new LoginRequest("a@test.com", "wrongpass"));
        Assert.Equal(AuthError.TempLocked, lockRes.Error);
        Assert.Equal(UserStatuses.TempLocked, user.Status);
        Assert.NotNull(user.LockoutEnd);
        Assert.True(user.LockoutEnd > DateTime.UtcNow.AddMinutes(14) && user.LockoutEnd <= DateTime.UtcNow.AddMinutes(15));

        // Kiểm tra email thông báo đã được gửi (không chứa mã OTP mở khóa)
        Assert.Contains(_emailSender.SentEmails, e => e.Purpose == "temp_locked_notice");
    }

    [Fact]
    public async Task Login_TempLockedUser_Within15Minutes_IsBlocked()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(10); // Còn 10 phút nữa

        // Kể cả gõ đúng mật khẩu vẫn bị chặn
        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.TempLocked, result.Error);
        Assert.Contains("tự động mở khóa sau", result.Message!);
    }

    [Fact]
    public async Task Login_TempLockedUser_After15Minutes_AutoUnlocksAndSucceeds()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // Đã quá 15 phút

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockoutEnd);
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
}
