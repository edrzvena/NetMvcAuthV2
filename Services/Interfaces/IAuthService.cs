using NetMvcAuthV2.DTOs.Auth;

namespace NetMvcAuthV2.Services.Interfaces;

public record AuthResult(bool Success, string? Error = null);

public interface IAuthService
{
    Task<AuthResult> RegisterAsync(RegisterDto dto);
    Task<AuthResult> LoginAsync(LoginDto dto);
    Task LogoutAsync();
    Task RequestPasswordResetAsync(string email, string resetLinkBase);
    Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto);
}
