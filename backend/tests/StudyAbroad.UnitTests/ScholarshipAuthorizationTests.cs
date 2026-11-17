using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-109] Moderator thêm, sửa, ẩn học bổng; Admin chỉ xem.</summary>
public class ScholarshipAuthorizationTests
{
    [Theory]
    [InlineData(nameof(AdminScholarshipsController.Create))]
    [InlineData(nameof(AdminScholarshipsController.Update))]
    [InlineData(nameof(AdminScholarshipsController.SetVisibility))]
    public void Writes_AreRestrictedToModerator(string method)
    {
        var roles = typeof(AdminScholarshipsController).GetMethod(method)?.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Equal(UserRoles.Moderator, roles);
    }

    [Fact]
    public void Controller_AllowsAdminAndModeratorToRead()
    {
        var roles = typeof(AdminScholarshipsController).GetCustomAttribute<AuthorizeAttribute>()!.Roles!.Split(',');

        Assert.Contains(UserRoles.Admin, roles);
        Assert.Contains(UserRoles.Moderator, roles);
    }

    [Fact]
    public void Controller_HasNoDeleteEndpoint()
    {
        var hasDelete = typeof(AdminScholarshipsController).GetMethods()
            .Any(m => m.GetCustomAttribute<Microsoft.AspNetCore.Mvc.HttpDeleteAttribute>() is not null);

        Assert.False(hasDelete);
    }

    [Fact]
    public void CreateOffering_IsRestrictedToModerator()
    {
        var roles = typeof(AdminUniversitiesController).GetMethod(nameof(AdminUniversitiesController.CreateOffering))
            ?.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Equal(UserRoles.Moderator, roles);
    }
}
