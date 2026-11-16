using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-65] Quyết định D2 bản 14/11: Moderator nhập thêm trường, Admin chỉ xem.</summary>
public class UniversityCreateAuthorizationTests
{
    private static string? RolesOfAction(string method) =>
        typeof(AdminUniversitiesController).GetMethod(method)?.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

    [Fact]
    public void Create_IsRestrictedToModerator()
    {
        Assert.Equal(UserRoles.Moderator, RolesOfAction(nameof(AdminUniversitiesController.Create)));
    }

    [Fact]
    public void Controller_StillAllowsAdminAndModeratorToRead()
    {
        // Attribute cấp controller cho cả hai role; các thao tác ghi tự thu hẹp bằng attribute riêng
        var controllerRoles = typeof(AdminUniversitiesController).GetCustomAttribute<AuthorizeAttribute>()?.Roles;
        Assert.Equal($"{UserRoles.Admin},{UserRoles.Moderator}", controllerRoles);

        foreach (var read in new[] { nameof(AdminUniversitiesController.Search), nameof(AdminUniversitiesController.GetById), nameof(AdminUniversitiesController.GetOfferings), nameof(AdminUniversitiesController.ListMajors) })
            Assert.Null(RolesOfAction(read));
    }
}
