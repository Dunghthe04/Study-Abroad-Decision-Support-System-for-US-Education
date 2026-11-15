using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-67] Quyết định D2 bản 14/11: Moderator ẩn hoặc hiện trường, Admin chỉ xem.</summary>
public class UniversityVisibilityAuthorizationTests
{
    [Fact]
    public void SetVisibility_IsRestrictedToModerator()
    {
        var roles = typeof(AdminUniversitiesController).GetMethod(nameof(AdminUniversitiesController.SetVisibility))
            ?.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Equal(UserRoles.Moderator, roles);
    }

    [Fact]
    public void Controller_StillAllowsAdminToReadUniversities()
    {
        var controllerRoles = typeof(AdminUniversitiesController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;

        Assert.Contains(UserRoles.Admin, controllerRoles!.Split(','));
        Assert.Contains(UserRoles.Moderator, controllerRoles.Split(','));
    }
}
