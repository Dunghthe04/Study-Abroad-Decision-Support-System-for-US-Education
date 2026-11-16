using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using StudyAbroad.Api.Controllers;
using StudyAbroad.Domain.Constants;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-66] Quyết định D2 bản 14/11: Moderator sửa dữ liệu trường (thông tin và bậc học), Admin chỉ xem.</summary>
public class UniversityUpdateAuthorizationTests
{
    private static string? RolesOfAction(string method) =>
        typeof(AdminUniversitiesController).GetMethod(method)?.GetCustomAttribute<AuthorizeAttribute>()?.Roles;

    [Fact]
    public void UpdateUniversity_IsRestrictedToModerator()
    {
        Assert.Equal(UserRoles.Moderator, RolesOfAction(nameof(AdminUniversitiesController.Update)));
    }

    [Fact]
    public void UpdateOffering_IsRestrictedToModerator()
    {
        Assert.Equal(UserRoles.Moderator, RolesOfAction(nameof(AdminUniversitiesController.UpdateOffering)));
    }

    [Fact]
    public void ReadActions_AreNotNarrowedByTheEditRestriction()
    {
        foreach (var read in new[] { nameof(AdminUniversitiesController.Search), nameof(AdminUniversitiesController.GetById), nameof(AdminUniversitiesController.GetOfferings) })
            Assert.Null(RolesOfAction(read));
    }
}
