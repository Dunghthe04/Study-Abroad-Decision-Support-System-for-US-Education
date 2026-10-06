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

        public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.ToList());
    }

    /// <summary>Not a real hash; lets tests check what was stored without running bcrypt.</summary>
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

    private AuthService CreateService() => new(_repo, new FakeHasher(), _tokens);

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
    public async Task Register_StudentOrParent_IsActive(string role, bool? parentAcknowledged)
    {
        var result = await CreateService().RegisterAsync(Request(role: role, parentAcknowledged: parentAcknowledged));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Active, result.User!.Status);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Register_Center_IsPending()
    {
        var result = await CreateService().RegisterAsync(Request(role: UserRoles.Center));

        Assert.Equal(UserStatuses.Pending, result.User!.Status);
        Assert.Single(_repo.Users);
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

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Ten <a@test.com>")]
    public async Task Register_InvalidEmail_IsValidationError(string email)
    {
        var result = await CreateService().RegisterAsync(Request(email: email));

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Fact]
    public async Task Register_ShortPassword_IsValidationError()
    {
        var result = await CreateService().RegisterAsync(Request(password: "1234567"));

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Register_BlankFullName_IsValidationError(string fullName)
    {
        var result = await CreateService().RegisterAsync(Request(fullName: fullName));

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Fact]
    public async Task Register_FullNameTooLong_IsValidationError()
    {
        var result = await CreateService().RegisterAsync(Request(fullName: new string('a', 129)));

        Assert.Equal(AuthError.Validation, result.Error);
    }

    [Fact]
    public async Task Register_DuplicateEmail_IsEmailTaken_EvenWithDifferentCase()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "a@test.com"));

        var result = await service.RegisterAsync(Request(email: "A@Test.com"));

        Assert.Equal(AuthError.EmailTaken, result.Error);
        Assert.Single(_repo.Users);
    }

    [Fact]
    public async Task Register_NormalizesEmailAndFullName()
    {
        await CreateService().RegisterAsync(Request(email: "  A@Test.COM ", fullName: "  Nguyen Van A  "));

        var stored = Assert.Single(_repo.Users);
        Assert.Equal("a@test.com", stored.Email);
        Assert.Equal("Nguyen Van A", stored.FullName);
    }

    [Fact]
    public async Task Register_StoresHashNotPlainPassword()
    {
        await CreateService().RegisterAsync(Request(password: "password123"));

        var stored = Assert.Single(_repo.Users);
        Assert.Equal("hashed:password123", stored.PasswordHash);
    }

    [Fact]
    public async Task Register_ReturnsDtoMatchingStoredUser()
    {
        var result = await CreateService().RegisterAsync(Request());

        var stored = Assert.Single(_repo.Users);
        Assert.Equal(stored.Id, result.User!.Id);
        Assert.Equal(stored.Email, result.User.Email);
        Assert.Equal(stored.Role, result.User.Role);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_CorrectPassword_ReturnsUser()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "a@test.com"));

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal("a@test.com", result.User!.Email);
    }

    [Fact]
    public async Task Login_EmailIsCaseInsensitive()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(email: "a@test.com"));

        var result = await service.LoginAsync(new LoginRequest("  A@Test.COM ", "password123"));

        Assert.Equal(AuthError.None, result.Error);
    }

    [Theory]
    [InlineData("a@test.com", "wrong-password")]
    [InlineData("nobody@test.com", "password123")]
    public async Task Login_WrongEmailOrPassword_IsInvalidCredentials(string email, string password)
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var result = await service.LoginAsync(new LoginRequest(email, password));

        Assert.Equal(AuthError.InvalidCredentials, result.Error);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Login_WrongEmailOrPassword_HaveSameMessage()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());

        var wrongPassword = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));
        var unknownEmail = await service.LoginAsync(new LoginRequest("nobody@test.com", "password123"));

        // Different messages would let anyone probe which emails are registered.
        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
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

    [Fact]
    public async Task Login_LockedAccount_WrongPassword_IsInvalidCredentials()
    {
        var service = CreateService();
        await service.RegisterAsync(Request());
        _repo.Users[0].Status = UserStatuses.Locked;

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "wrong-password"));

        // Only someone who knows the password may learn that the account is locked.
        Assert.Equal(AuthError.InvalidCredentials, result.Error);
    }

    [Fact]
    public async Task Login_PendingCenter_CanLogin()
    {
        var service = CreateService();
        await service.RegisterAsync(Request(role: UserRoles.Center));

        var result = await service.LoginAsync(new LoginRequest("a@test.com", "password123"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal(UserStatuses.Pending, result.User!.Status);
    }

    [Fact]
    public async Task Register_WithPhone_StoresPhone()
    {
        var service = CreateService();
        var result = await service.RegisterAsync(Request(phone: "0912345678"));

        Assert.Equal(AuthError.None, result.Error);
        Assert.Equal("0912345678", result.User!.Phone);
        Assert.Equal("0912345678", _repo.Users[0].Phone);
    }

    [Fact]
    public async Task Register_WithPhoneTooLong_ReturnsValidationError()
    {
        var service = CreateService();
        var longPhone = new string('1', 33);
        var result = await service.RegisterAsync(Request(phone: longPhone));

        Assert.Equal(AuthError.Validation, result.Error);
        Assert.Contains("Số điện thoại", result.Message!);
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
}

