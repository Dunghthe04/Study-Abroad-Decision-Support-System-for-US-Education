using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.UnitTests;

/// <summary>Runs real bcrypt, so it catches mistakes such as swapped arguments that the fake hasher cannot.</summary>
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_DoesNotReturnPlainPassword()
    {
        var hash = _hasher.Hash("password123");

        Assert.NotEqual("password123", hash);
        Assert.StartsWith("$2", hash);
        Assert.Equal(60, hash.Length);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash("password123");

        Assert.True(_hasher.VerifyPassword(hash, "password123"));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _hasher.Hash("password123");

        Assert.False(_hasher.VerifyPassword(hash, "wrong-password"));
    }
}
