using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NetMvcAuthV2.Constants;
using NetMvcAuthV2.DTOs.Auth;
using NetMvcAuthV2.Services.Interfaces;

namespace NetMvcAuthV2.Controllers;

public class AccountController(IAuthService authService) : Controller
{
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new RegisterDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Register(RegisterDto dto)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (ModelState.IsValid)
        {
            var result = await authService.RegisterAsync(dto);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Registrasi berhasil, silakan login.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(nameof(dto.Email), result.Error!);
        }

        dto.Password = dto.ConfirmPassword = string.Empty;
        return View(dto);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginDto());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<IActionResult> Login(LoginDto dto, string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        if (ModelState.IsValid)
        {
            var result = await authService.LoginAsync(dto);
            if (result.Success)
            {
                return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? LocalRedirect(returnUrl)
                    : RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        dto.Password = string.Empty;
        ViewData["ReturnUrl"] = returnUrl;
        return View(dto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await authService.LogoutAsync();
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordDto());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.ForgotPassword)]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
    {
        if (!ModelState.IsValid)
            return View(dto);

        var resetLinkBase = Url.Action(nameof(ResetPassword), "Account", values: null, protocol: Request.Scheme)!;
        await authService.RequestPasswordResetAsync(dto.Email, resetLinkBase);

        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    public IActionResult ForgotPasswordConfirmation() => View();

    [HttpGet]
    public IActionResult ResetPassword(string? token = null)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return View(new ResetPasswordDto { Token = token ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";

        if (ModelState.IsValid)
        {
            var result = await authService.ResetPasswordAsync(dto);
            if (result.Success)
            {
                TempData["SuccessMessage"] = "Password berhasil diubah, silakan login.";
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        dto.NewPassword = dto.ConfirmPassword = string.Empty;
        return View(dto);
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}
