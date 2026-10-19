using System.Security.Claims;
using NetMvcAuthV2.Common.Enums;
using NetMvcAuthV2.DTOs.Auth;
using NetMvcAuthV2.Models;

namespace NetMvcAuthV2.Mappings;

public static class UserMappings
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    // PasswordHash diisi oleh service; role selalu default, tidak pernah dari input.
    public static User ToUser(this RegisterDto dto) => new()
    {
        Email = NormalizeEmail(dto.Email),
        FullName = dto.FullName.Trim(),
        Role = UserRole.User
    };

    public static ClaimsPrincipal ToPrincipal(this User user, string authenticationType)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new(CustomClaimTypes.SecurityStamp, user.SecurityStamp)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType));
    }
}

public static class CustomClaimTypes
{
    public const string SecurityStamp = "security_stamp";
}
