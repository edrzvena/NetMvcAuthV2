using System.ComponentModel.DataAnnotations;
using NetMvcAuthV2.Constants;

namespace NetMvcAuthV2.DTOs.Auth;

public class ResetPasswordDto
{
    [Required(ErrorMessage = "Token reset password tidak valid atau sudah kedaluwarsa.")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password baru wajib diisi.")]
    [DataType(DataType.Password)]
    [StringLength(AuthSettings.MaxPasswordLength, MinimumLength = AuthSettings.MinPasswordLength,
        ErrorMessage = "Password harus 8–128 karakter.")]
    [RegularExpression(AuthSettings.PasswordPattern, ErrorMessage = "Password harus mengandung huruf dan angka.")]
    [Display(Name = "Password Baru")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ulangi password baru wajib diisi.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Password gak sama.")]
    [Display(Name = "Ulangi Password Baru")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
