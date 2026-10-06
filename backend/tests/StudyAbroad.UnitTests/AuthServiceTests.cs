using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class AuthServiceTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];
        public List<UserSession> Sessions { get; } = [];
        public List<OtpToken> Otps { get; } = [];

        public Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(u => u.Email == email));

        public Task<bool> PhoneExistAsync(string phone, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(u => u.Phone == phone));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

        public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
        {
            var idx = Users.FindIndex(u => u.Id == user.Id);
            if (idx >= 0) Users[idx] = user;
            return Task.CompletedTask;
        }

        public Task CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
        {
            Sessions.Add(session);
            return Task.CompletedTask;
        }

        public Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sessions.FirstOrDefault(s => s.TokenHash == tokenHash));

        public Task RevokeSessionAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            var s = Sessions.FirstOrDefault(x => x.TokenHash == tokenHash);
            if (s != null) s.IsRevoked = true;
            return Task.CompletedTask;
        }

        public Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            foreach (var s in Sessions.Where(x => x.UserId == userId))
            {
                s.IsRevoked = true;
            }
            return Task.CompletedTask;
        }

        public Task SaveOtpAsync(OtpToken token, CancellationToken cancellationToken = default)
        {
            var idx = Otps.FindIndex(o => o.Id == token.Id);
            if (idx >= 0) Otps[idx] = token;
            else Otps.Add(token);
            return Task.CompletedTask;
        }

        public Task<OtpToken?> GetLatestOtpAsync(Guid userId, string purpose, CancellationToken cancellationToken = default) =>
            Task.FromResult(Otps
                .Where(o => o.UserId == userId && o.Purpose == purpose && o.UsedAt == null)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefault());

        public Task InvalidateOtpsAsync(Guid userId, string purpose, CancellationToken cancellationToken = default)
        {
            foreach (var o in Otps.Where(x => x.UserId == userId && x.Purpose == purpose && x.UsedAt == null))
            {
                o.UsedAt = DateTime.UtcNow;
            }
            return Task.CompletedTask;
        }

        public Task UpdateOtpAsync(OtpToken token, CancellationToken cancellationToken = default)
        {
            var idx = Otps.FindIndex(o => o.Id == token.Id);
            if (idx >= 0) Otps[idx] = token;
            return Task.CompletedTask;
        }

        public Task<int> CountOtpRequestsInLastHourAsync(Guid userId, string purpose, CancellationToken cancellationToken = default)
        {
            var oneHourAgo = DateTime.UtcNow.AddHours(-1);
            return Task.FromResult(Otps.Count(o => o.UserId == userId && o.Purpose == purpose && o.CreatedAt >= oneHourAgo));
        }

        public Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var expired = Users.Where(u => u.Status == UserStatuses.TempLocked && u.LockoutEnd != null && u.LockoutEnd <= now).ToList();
            foreach (var u in expired)
            {
                u.Status = u.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
                u.FailedLoginAttempts = 0;
                u.LockoutEnd = null;
            }
            return Task.FromResult(expired.Count);
        }

        public Task<int> CleanupExpiredOtpsAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var count = Otps.RemoveAll(o => o.ExpiresAt < now);
            return Task.FromResult(count);
        }

        public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.ToList());
    }

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

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Purpose, string Code)> SentEmails { get; } = [];

        public Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default)
        {
            SentEmails.Add((toEmail, OtpPurposes.VerifyEmail, otpCode));
            return Task.CompletedTask;
        }

        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default)
        {
            SentEmails.Add((toEmail, OtpPurposes.ResetPassword, otpCode));
            return Task.CompletedTask;
        }

        public Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 15, CancellationToken ct = default)
        {
            SentEmails.Add((toEmail, "temp_locked_notice", $"lockout:{lockoutMinutes}m"));
            return Task.CompletedTask;
        }
    }

    private readonly FakeUserRepository _repo = new();
    private readonly FakeTokenService _tokens = new();
    private readonly FakeEmailSender _emailSender = new();

    private AuthService CreateService() => new(_repo, new FakeHasher(), _tokens, _emailSender);

    private static RegisterRequest Request(
        string email = "a@test.com",
        string password = "password123",
        string fullName = "Nguyen Van A",
        string role = UserRoles.Parent,
        bool? parentAcknowledged = null,
        string? phone = null) =>
        new(email, password, fullName, role, parentAcknowledged, phone);

    [Theory]
    [InlineData(UserRoles.Parent, null)]
    [InlineData(UserRoles.Student, true)]
    public async Task Register_StudentOrParent_IsUnverifiedAndSendsOtpEmail(string role, bool? parentAcknowledged)
    {
        var result = await CreateService().RegisterAsync(Request(role: role, parentAcknowledged: parentAcknowledged));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Unverified, result.User!.Status);
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
    public async Task VerifyEmail_ExpiredOtp_ReturnsOtpExpiredError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var otp = _repo.Otps[0];
        otp.ExpiresAt = DateTime.UtcNow.AddMinutes(-1); // Hết hạn

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", _emailSender.SentEmails[0].Code));

        Assert.Equal(AuthError.OtpExpired, result.Error);
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
    public async Task Register_Center_IsPendingAfterEmailVerification()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(role: UserRoles.Center));
        var otpCode = _emailSender.SentEmails[0].Code;

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", otpCode));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Pending, _repo.Users[0].Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task Register_StudentWithoutParentAcknowledged_IsValidationError(bool? parentAcknowledged)
    {
        var result = await CreateService().RegisterAsync(Request(role: UserRoles.Student, parentAcknowledged: parentAcknowledged));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Theory]
    [InlineData(UserRoles.Admin)]
    [InlineData("teacher")]
    [InlineData("")]
    public async Task Register_RoleNotAllowed_IsValidationError(string role)
    {
        var result = await CreateService().RegisterAsync(Request(role: role));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Empty(_repo.Users);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsEmailTakenError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var result = await service.RegisterAsync(Request());

        Assert.Equal(AuthError.EmailTaken, result.Error);
        Assert.Single(_repo.Users);
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
    public async Task UnlockAccount_After15Minutes_RestoresActiveStatus()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        var user = _repo.Users[0];
        user.Status = UserStatuses.TempLocked;
        user.FailedLoginAttempts = 5;
        user.LockoutEnd = DateTime.UtcNow.AddMinutes(-1); // Đã hết 15 phút

        var unlockResult = await service.UnlockAccountAsync(new UnlockAccountRequest("a@test.com", ""));

        Assert.Equal(AuthError.None, unlockResult.Error);
        Assert.Equal(UserStatuses.Active, user.Status);
        Assert.Equal(0, user.FailedLoginAttempts);

        // Sau khi mở khóa -> Đăng nhập thành công
        var loginResult = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));
        Assert.Equal(AuthError.None, loginResult.Error);
    }

    [Fact]
    public async Task UnlockAccount_AdminLockedUser_CannotUnlockViaOtp()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Locked; // Admin khóa vĩnh viễn

        var result = await service.UnlockAccountAsync(new UnlockAccountRequest("a@test.com", "123456"));

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

        // Session cũ đã bị thu hồi
        Assert.True(_repo.Sessions[0].IsRevoked);

        // Mật khẩu mới đăng nhập được
        var newLogin = await service.LoginAsync(new LoginRequest("a@test.com", "newPassword123"));
        Assert.Equal(AuthError.None, newLogin.Error);
    }

    [Fact]
    public async Task ResendOtp_Within60Seconds_EnforcesCooldown()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        // Yêu cầu gửi lại ngay lập tức
        var res = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.Validation, res.Error);
        Assert.Contains("60 giây", res.Message!);
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
        Assert.Equal(expectedPhone, result.User!.Phone);
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
        var reg = await service.RegisterAsync(Request(phone: "0987654321"));
        var userId = reg.User!.Id;

        var me = await service.GetMeAsync(userId);

        Assert.NotNull(me);
        Assert.Equal(userId, me.Id);
        Assert.Equal("0987654321", me.Phone);
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
    public async Task Register_WithDuplicatePhone_ReturnsPhoneTakenError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "user1@test.com", phone: "0912345678"));

        var result = await service.RegisterAsync(Request(email: "user2@test.com", phone: "0912345678"));

        Assert.Equal(AuthError.PhoneTaken, result.Error);
        Assert.Contains("Số điện thoại đã được sử dụng", result.Message!);
    }

    [Fact]
    public async Task Register_Generates5MinuteOtp_AndSendsVerificationEmail()
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request());

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Unverified, result.User!.Status);
        Assert.Single(_repo.Otps);
        var otp = _repo.Otps[0];
        Assert.Equal(OtpPurposes.VerifyEmail, otp.Purpose);
        // Kiểm tra thời hạn 5 phút
        Assert.True(otp.ExpiresAt > DateTime.UtcNow.AddMinutes(4) && otp.ExpiresAt <= DateTime.UtcNow.AddMinutes(5));
        Assert.Single(_emailSender.SentEmails);
        Assert.Equal("a@test.com", _emailSender.SentEmails[0].To);
    }

    [Fact]
    public async Task VerifyEmail_OtpExpiredAfter5Minutes_ReturnsOtpExpiredError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        // Giả lập OTP đã hết hạn sau 5 phút
        _repo.Otps[0].ExpiresAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "123456"));

        Assert.Equal(AuthError.OtpExpired, result.Error);
        Assert.Contains("hết hạn", result.Message!);
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
            Assert.Contains($"còn {5 - i} lần thử", res.Message!);
            Assert.Equal(i, _repo.Otps[0].Attempts);
            Assert.Null(_repo.Otps[0].UsedAt);
            Assert.Equal(UserStatuses.Unverified, user.Status);
            Assert.Equal(0, user.FailedLoginAttempts); // Không bao giờ tăng FailedLoginAttempts
        }

        // Nhập sai lần thứ 5 -> Mã OTP bị vô hiệu hóa
        var finalRes = await service.VerifyEmailAsync(new VerifyEmailRequest("a@test.com", "000000"));
        Assert.Equal(AuthError.OtpInvalid, finalRes.Error);
        Assert.Contains("vô hiệu hóa", finalRes.Message!);
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
    public async Task ResendOtp_Within60Seconds_ReturnsValidationError()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var result = await service.ResendOtpAsync(new ResendOtpRequest("a@test.com", OtpPurposes.VerifyEmail));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("Vui lòng đợi thêm", result.Message!);
    }

    [Fact]
    public async Task ResendOtp_MoreThan5TimesPerHour_ReturnsQuotaExceededError()
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

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("vượt quá giới hạn 5 lần", result.Message!);
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
}
