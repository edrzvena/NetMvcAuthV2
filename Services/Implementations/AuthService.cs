using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NetMvcAuthV2.Constants;
using NetMvcAuthV2.DTOs.Auth;
using NetMvcAuthV2.Mappings;
using NetMvcAuthV2.Models;
using NetMvcAuthV2.Repositories.Interfaces;
using NetMvcAuthV2.Services.Interfaces;

namespace NetMvcAuthV2.Services.Implementations;

public class AuthService(
    IUserRepository users,
    IPasswordHasher<User> passwordHasher,
    IEmailSender emailSender,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuthService> logger) : IAuthService
{
    private const string EmailTakenError = "Email sudah terdaftar.";
    private const string InvalidCredentialsError = "Email atau password salah.";
    private const string LockedOutError = "Terlalu banyak percobaan. Coba lagi beberapa menit lagi.";
    private const string InvalidTokenError = "Token reset password tidak valid atau sudah kedaluwarsa.";

    // Dipakai untuk menyamakan waktu respons saat email tidak dikenal.
    private readonly string _dummyHash = passwordHasher.HashPassword(new User(), "dummy-Password-1");

    public async Task<AuthResult> RegisterAsync(RegisterDto dto)
    {
        var email = UserMappings.NormalizeEmail(dto.Email);
        if (await users.EmailExistsAsync(email))
            return new AuthResult(false, EmailTakenError);

        var user = dto.ToUser();
        user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        try
        {
            await users.AddAsync(user);
            await users.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Balapan dua register bersamaan: unique index di DB yang menang.
            if (await users.EmailExistsAsync(email))
                return new AuthResult(false, EmailTakenError);
            throw;
        }

        return new AuthResult(true);
    }

    public async Task<AuthResult> LoginAsync(LoginDto dto)
    {
        var email = UserMappings.NormalizeEmail(dto.Email);
        var user = await users.GetByEmailAsync(email);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(new User(), _dummyHash, dto.Password);
            logger.LogWarning("Login gagal: email tidak dikenal ({Email})", email);
            return new AuthResult(false, InvalidCredentialsError);
        }

        var now = DateTime.UtcNow;

        if (user.LockoutEndAt is { } lockoutEnd)
        {
            if (lockoutEnd > now)
            {
                logger.LogWarning("Login ditolak: akun terkunci (UserId {UserId})", user.Id);
                return new AuthResult(false, LockedOutError);
            }

            // Lockout sudah lewat: mulai hitungan dari nol.
            user.LockoutEndAt = null;
            user.FailedLoginCount = 0;
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            var locked = user.FailedLoginCount >= AuthSettings.MaxFailedAttempts;
            if (locked)
                user.LockoutEndAt = now.AddMinutes(AuthSettings.LockoutMinutes);

            users.Update(user);
            await users.SaveChangesAsync();

            logger.LogWarning("Login gagal: password salah (UserId {UserId}, percobaan {Count})",
                user.Id, user.FailedLoginCount);
            if (locked)
                logger.LogWarning("Akun dikunci sampai {LockoutEnd:u} (UserId {UserId})", user.LockoutEndAt, user.Id);

            return new AuthResult(false, locked ? LockedOutError : InvalidCredentialsError);
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwordHasher.HashPassword(user, dto.Password);

        user.FailedLoginCount = 0;
        user.LockoutEndAt = null;
        users.Update(user);
        await users.SaveChangesAsync();

        var properties = new AuthenticationProperties { IsPersistent = dto.RememberMe };
        if (dto.RememberMe)
            properties.ExpiresUtc = now.AddDays(14);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            user.ToPrincipal(CookieAuthenticationDefaults.AuthenticationScheme),
            properties);

        return new AuthResult(true);
    }

    public Task LogoutAsync() =>
        HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    public async Task RequestPasswordResetAsync(string email, string resetLinkBase)
    {
        var user = await users.GetByEmailAsync(UserMappings.NormalizeEmail(email));
        if (user is null)
            return;

        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        user.ResetPasswordTokenHash = HashToken(token);
        user.PasswordResetTokenExpireAt = DateTime.UtcNow.Add(AuthSettings.ResetTokenLifetime);
        users.Update(user);
        await users.SaveChangesAsync();

        var link = $"{resetLinkBase}?token={WebUtility.UrlEncode(token)}";
        var body = $"""
            <p>Halo {WebUtility.HtmlEncode(user.FullName)},</p>
            <p>Klik link ini buat bikin password baru (berlaku 1 jam):</p>
            <p><a href="{link}">{link}</a></p>
            <p>Kalau lo gak minta reset password, abaikan aja email ini.</p>
            """;

        try
        {
            await emailSender.SendAsync(user.Email, "Reset password", body);
        }
        catch (Exception ex)
        {
            // Jangan bocorkan lewat error: respons ke user harus sama baik email ada maupun tidak.
            logger.LogError(ex, "Gagal kirim email reset password (UserId {UserId})", user.Id);
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordDto dto)
    {
        var user = await users.GetByResetTokenHashAsync(HashToken(dto.Token));

        if (user is null
            || user.PasswordResetTokenExpireAt is not { } expireAt
            || expireAt < DateTime.UtcNow)
        {
            return new AuthResult(false, InvalidTokenError);
        }

        user.PasswordHash = passwordHasher.HashPassword(user, dto.NewPassword);
        user.ResetPasswordTokenHash = null;
        user.PasswordResetTokenExpireAt = null;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        users.Update(user);
        await users.SaveChangesAsync();

        logger.LogInformation("Reset password berhasil (UserId {UserId})", user.Id);
        return new AuthResult(true);
    }

    private HttpContext HttpContext =>
        httpContextAccessor.HttpContext ?? throw new InvalidOperationException("Tidak ada HttpContext aktif.");

    private static string HashToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
