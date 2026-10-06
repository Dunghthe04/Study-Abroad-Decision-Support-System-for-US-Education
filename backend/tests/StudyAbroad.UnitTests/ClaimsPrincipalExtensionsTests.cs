using System.Security.Claims;
using StudyAbroad.Api.Extensions;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

public class ClaimsPrincipalExtensionsTests
{
    [Fact]
    public void GetUserId_WithValidGuidClaim_ReturnsGuid()
    {
        var expectedId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, expectedId.ToString())
        ], "TestAuth"));

        var result = principal.GetUserId();

        Assert.Equal(expectedId, result);
    }

    [Fact]
    public void GetUserId_WithMissingClaim_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        var result = principal.GetUserId();

        Assert.Null(result);
    }

    [Fact]
    public void GetUserId_WithInvalidGuidClaim_ReturnsNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "invalid-guid-string")
        ], "TestAuth"));

        var result = principal.GetUserId();

        Assert.Null(result);
    }

    [Fact]
    public void GetEmail_WithEmailClaim_ReturnsEmail()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Email, "student@test.com")
        ], "TestAuth"));

        var result = principal.GetEmail();

        Assert.Equal("student@test.com", result);
    }

    [Fact]
    public void GetRole_WithRoleClaim_ReturnsRole()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, UserRoles.Student)
        ], "TestAuth"));

        var result = principal.GetRole();

        Assert.Equal(UserRoles.Student, result);
    }

    [Fact]
    public void IsAdmin_WhenUserIsAdmin_ReturnsTrue()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, UserRoles.Admin)
        ], "TestAuth"));

        Assert.True(principal.IsAdmin());
    }

    [Theory]
    [InlineData(UserRoles.Student)]
    [InlineData(UserRoles.Parent)]
    [InlineData(UserRoles.Center)]
    [InlineData("random_role")]
    public void IsAdmin_WhenUserIsNotAdmin_ReturnsFalse(string role)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

        Assert.False(principal.IsAdmin());
    }
}
