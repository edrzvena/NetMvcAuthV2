using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetMvcAuthV2.Common.Enums;
using NetMvcAuthV2.Constants;
using NetMvcAuthV2.DTOs.Auth;
using NetMvcAuthV2.Models;
using NetMvcAuthV2.Repositories.Interfaces;
using NetMvcAuthV2.Services.Implementations;
using NetMvcAuthV2.Services.Interfaces;
using NSubstitute;

namespace NetMvcAuthV2.Tests;

public class AuthServiceTests
{
    private const string GenericLoginError = "Email atau password salah.";
    private const string LockedOutError = "Terlalu banyak percobaan. Coba lagi beberapa menit lagi.";
    private const string InvalidTokenError = "Token reset password tidak valid atau sudah kedaluwarsa.";

    private readonly List<User> _store = [];
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IAuthenticationService _authentication = Substitute.For<IAuthenticationService>();
    private readonly PasswordHasher<User> _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _users.GetByEmailAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(_store.FirstOrDefault(u => u.Email == call.Arg<string>())));
        _users.EmailExistsAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(_store.Any(u => u.Email == call.Arg<string>())));
        _users.GetByResetTokenHashAsync(Arg.Any<string>())
            .Returns(call => Task.FromResult(_store.FirstOrDefault(u => u.ResetPasswordTokenHash == call.Arg<string>())));
        _users.AddAsync(Arg.Do<User>(u => _store.Add(u)));

        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection()
                .AddSingleton(_authentication)
                .BuildServiceProvider()
        };
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        _service = new AuthService(_users, _hasher, _emailSender, accessor, NullLogger<AuthService>.Instance);
    }

    private User AddUser(string email = "budi@example.com", string password = "Password1", UserRole role = UserRole.User)
    {
        var user = new User { Id = 7, Email = email, FullName = "Budi Santoso", Role = role };
        user.PasswordHash = _hasher.HashPassword(user, password);
        _store.Add(user);
        return user;
    }

    private static string Sha256Hex(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static LoginDto Login(string email, string password, bool rememberMe = false) =>
        new() { Email = email, Password = password, RememberMe = rememberMe };

    // ---------- Register ----------

    [Fact]
    public async Task Register_NewEmail_SavesUserWithHashedPasswordAndDefaultRole()
    {
        var result = await _service.RegisterAsync(new RegisterDto
        {
            Email = "Budi@Example.com",
            FullName = "Budi",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        Assert.True(result.Success);
        var user = Assert.Single(_store);
        Assert.Equal("budi@example.com", user.Email);
        Assert.NotEqual("Password1", user.PasswordHash);
        Assert.Equal(UserRole.User, user.Role);
        await _users.Received(1).SaveChangesAsync();
    }

    [Fact]
    public async Task Register_DuplicateEmailWithDifferentCase_Fails()
    {
        AddUser("budi@example.com");

        var result = await _service.RegisterAsync(new RegisterDto
        {
            Email = "  BUDI@example.com ",
            FullName = "Budi 2",
            Password = "Password1",
            ConfirmPassword = "Password1"
        });

        Assert.False(result.Success);
        Assert.Equal("Email sudah terdaftar.", result.Error);
        Assert.Single(_store);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_CorrectCredentials_SignsInWithFullClaims()
    {
        AddUser();
        ClaimsPrincipal? principal = null;
        await _authentication.SignInAsync(
            Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Do<ClaimsPrincipal>(p => principal = p), Arg.Any<AuthenticationProperties>());

        var result = await _service.LoginAsync(Login("budi@example.com", "Password1"));

        Assert.True(result.Success);
        Assert.NotNull(principal);
        Assert.Equal("7", principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("budi@example.com", principal.FindFirstValue(ClaimTypes.Email));
        Assert.Equal("Budi Santoso", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("User", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_RememberMe_ControlsPersistentCookie(bool rememberMe)
    {
        AddUser();
        AuthenticationProperties? properties = null;
        await _authentication.SignInAsync(
            Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Do<AuthenticationProperties>(p => properties = p));

        await _service.LoginAsync(Login("budi@example.com", "Password1", rememberMe));

        Assert.NotNull(properties);
        Assert.Equal(rememberMe, properties.IsPersistent);
    }

    [Fact]
    public async Task Login_WrongPassword_FailsWithGenericMessageAndIncrementsCounter()
    {
        var user = AddUser();

        var result = await _service.LoginAsync(Login("budi@example.com", "SalahTotal9"));

        Assert.False(result.Success);
        Assert.Equal(GenericLoginError, result.Error);
        Assert.Equal(1, user.FailedLoginCount);
    }

    [Fact]
    public async Task Login_UnknownEmail_ReturnsSameMessageAsWrongPassword()
    {
        AddUser();

        var unknown = await _service.LoginAsync(Login("nobody@example.com", "Password1"));
        var wrongPassword = await _service.LoginAsync(Login("budi@example.com", "SalahTotal9"));

        Assert.False(unknown.Success);
        Assert.Equal(wrongPassword.Error, unknown.Error);
    }

    [Fact]
    public async Task Login_FifthWrongAttempt_LocksAccountEvenForCorrectPassword()
    {
        var user = AddUser();

        for (var i = 0; i < AuthSettings.MaxFailedAttempts; i++)
            await _service.LoginAsync(Login("budi@example.com", "SalahTotal9"));

        Assert.NotNull(user.LockoutEndAt);
        Assert.True(user.LockoutEndAt > DateTime.UtcNow);

        var result = await _service.LoginAsync(Login("budi@example.com", "Password1"));

        Assert.False(result.Success);
        Assert.Equal(LockedOutError, result.Error);
        await _authentication.DidNotReceive().SignInAsync(
            Arg.Any<HttpContext>(), Arg.Any<string>(), Arg.Any<ClaimsPrincipal>(), Arg.Any<AuthenticationProperties>());
    }

    [Fact]
    public async Task Login_AfterLockoutExpired_SucceedsAndResetsCounter()
    {
        var user = AddUser();
        user.FailedLoginCount = AuthSettings.MaxFailedAttempts;
        user.LockoutEndAt = DateTime.UtcNow.AddMinutes(-1);

        var result = await _service.LoginAsync(Login("budi@example.com", "Password1"));

        Assert.True(result.Success);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Null(user.LockoutEndAt);
    }

    // ---------- Forgot password ----------

    [Fact]
    public async Task RequestPasswordReset_ExistingEmail_StoresTokenHashAndSendsOneEmail()
    {
        var user = AddUser();
        string? body = null;
        await _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Do<string>(b => body = b));

        await _service.RequestPasswordResetAsync("BUDI@example.com", "https://localhost/Account/ResetPassword");

        Assert.NotNull(user.ResetPasswordTokenHash);
        Assert.Equal(64, user.ResetPasswordTokenHash.Length);
        Assert.NotNull(user.PasswordResetTokenExpireAt);
        Assert.InRange(user.PasswordResetTokenExpireAt.Value,
            DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
        await _emailSender.Received(1).SendAsync("budi@example.com", Arg.Any<string>(), Arg.Any<string>());

        // Yang tersimpan hanya hash; token mentah hanya ada di link email.
        Assert.NotNull(body);
        Assert.DoesNotContain(user.ResetPasswordTokenHash, body);
        var token = body.Split("?token=")[1].Split('"')[0];
        Assert.Equal(user.ResetPasswordTokenHash, Sha256Hex(token));
    }

    [Fact]
    public async Task RequestPasswordReset_UnknownEmail_DoesNothing()
    {
        AddUser();

        var exception = await Record.ExceptionAsync(() =>
            _service.RequestPasswordResetAsync("nobody@example.com", "https://localhost/Account/ResetPassword"));

        Assert.Null(exception);
        await _emailSender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task RequestPasswordReset_EmailSenderThrows_DoesNotLeakError()
    {
        AddUser();
        _emailSender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromException(new InvalidOperationException("smtp down")));

        var exception = await Record.ExceptionAsync(() =>
            _service.RequestPasswordResetAsync("budi@example.com", "https://localhost/Account/ResetPassword"));

        Assert.Null(exception);
    }

    // ---------- Reset password ----------

    private User AddUserWithResetToken(string token, TimeSpan validFor)
    {
        var user = AddUser();
        user.ResetPasswordTokenHash = Sha256Hex(token);
        user.PasswordResetTokenExpireAt = DateTime.UtcNow.Add(validFor);
        return user;
    }

    private static ResetPasswordDto ResetDto(string token, string password = "PasswordBaru1") =>
        new() { Token = token, NewPassword = password, ConfirmPassword = password };

    [Fact]
    public async Task ResetPassword_ValidToken_ChangesPasswordClearsTokenAndRotatesStamp()
    {
        var user = AddUserWithResetToken("token-valid", TimeSpan.FromMinutes(30));
        var oldStamp = user.SecurityStamp;

        var result = await _service.ResetPasswordAsync(ResetDto("token-valid"));

        Assert.True(result.Success);
        Assert.Equal(PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(user, user.PasswordHash, "PasswordBaru1"));
        Assert.Null(user.ResetPasswordTokenHash);
        Assert.Null(user.PasswordResetTokenExpireAt);
        Assert.NotEqual(oldStamp, user.SecurityStamp);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_FailsWithGenericMessage()
    {
        AddUserWithResetToken("token-expired", TimeSpan.FromMinutes(-1));

        var result = await _service.ResetPasswordAsync(ResetDto("token-expired"));

        Assert.False(result.Success);
        Assert.Equal(InvalidTokenError, result.Error);
    }

    [Fact]
    public async Task ResetPassword_WrongToken_FailsWithGenericMessage()
    {
        AddUserWithResetToken("token-asli", TimeSpan.FromMinutes(30));

        var result = await _service.ResetPasswordAsync(ResetDto("token-ngawur"));

        Assert.False(result.Success);
        Assert.Equal(InvalidTokenError, result.Error);
    }

    [Fact]
    public async Task ResetPassword_TokenAlreadyUsed_FailsOnSecondAttempt()
    {
        AddUserWithResetToken("token-sekali", TimeSpan.FromMinutes(30));

        var first = await _service.ResetPasswordAsync(ResetDto("token-sekali"));
        var second = await _service.ResetPasswordAsync(ResetDto("token-sekali", "LainLagi2"));

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Equal(InvalidTokenError, second.Error);
    }
}
