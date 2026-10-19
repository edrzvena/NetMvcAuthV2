using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using NetMvcAuthV2.Mappings;
using NetMvcAuthV2.Repositories.Interfaces;

namespace NetMvcAuthV2.Common.Security;

public static class SecurityStampCookieValidator
{
    // Menolak cookie yang SecurityStamp-nya sudah beda dengan di DB (mis. setelah reset password).
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var stamp = context.Principal?.FindFirstValue(CustomClaimTypes.SecurityStamp);
        var idClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (stamp is null || !int.TryParse(idClaim, out var userId))
        {
            await Reject(context);
            return;
        }

        var users = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId);

        if (user is null || user.SecurityStamp != stamp)
            await Reject(context);
    }

    private static async Task Reject(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
