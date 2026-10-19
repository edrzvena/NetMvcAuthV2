using System.Security.Claims;
using NetMvcAuthV2.Common.Enums;
using NetMvcAuthV2.DTOs.Auth;
using NetMvcAuthV2.Mappings;
using NetMvcAuthV2.Models;

namespace NetMvcAuthV2.Tests;

public class UserMappingsTests
{
    [Fact]
    public void RegisterDto_ToUser_NormalizesEmailTrimsNameAndUsesDefaultRole()
    {
        var dto = new RegisterDto
        {
            Email = "  Budi@Example.COM ",
            FullName = "  Budi Santoso  ",
            Password = "Password1",
            ConfirmPassword = "Password1"
        };

        var user = dto.ToUser();

        Assert.Equal("budi@example.com", user.Email);
        Assert.Equal("Budi Santoso", user.FullName);
        Assert.Equal(UserRole.User, user.Role);
    }

    [Fact]
    public void RegisterDto_HasNoRoleProperty_SoRoleCannotComeFromForm()
    {
        Assert.Null(typeof(RegisterDto).GetProperty("Role"));
    }

    [Fact]
    public void User_ToPrincipal_ContainsAllExpectedClaims()
    {
        var user = new User { Id = 42, Email = "admin@example.com", FullName = "Admin", Role = UserRole.Admin };

        var principal = user.ToPrincipal("Cookies");

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal("42", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Admin", principal.FindFirstValue(ClaimTypes.Role));
        Assert.Equal(user.SecurityStamp, principal.FindFirstValue(CustomClaimTypes.SecurityStamp));
    }
}
