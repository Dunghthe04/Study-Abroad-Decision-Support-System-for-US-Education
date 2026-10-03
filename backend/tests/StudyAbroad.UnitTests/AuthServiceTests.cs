using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class AuthServiceTests
{
    private sealed class FakeUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];

        public Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.Any(u => u.Email == email));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Users.Add(user);
            return Task.CompletedTask;
        }

        public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == email));
    }

    /// <summary>Not a real hash; lets tests check what was stored without running bcrypt.</summary>
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;
        public bool VerifyPassword(string hash, string password) => hash == "hashed:" + password;
    }

    private readonly FakeUserRepository _repo = new();

    private AuthService CreateService() => new(_repo, new FakeHasher());

    private static RegisterRequest Request(
        string email = "a@test.com",
        string password = "password123",
        string fullName = "Nguyen Van A",
        string role = UserRoles.Parent,
        bool? parentAcknowledged = null) =>
        new(email, password, fullName, role, parentAcknowledged);

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
}
