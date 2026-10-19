using System.ComponentModel.DataAnnotations;
using NetMvcAuthV2.Constants;

namespace NetMvcAuthV2.DTOs.Auth;

public class RegisterDto
{
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email gak valid.")]
    [MaxLength(256, ErrorMessage = "Email maksimal 256 karakter.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Nama lengkap wajib diisi.")]
    [MaxLength(100, ErrorMessage = "Nama lengkap maksimal 100 karakter.")]
    [Display(Name = "Nama Lengkap")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password wajib diisi.")]
    [DataType(DataType.Password)]
    [StringLength(AuthSettings.MaxPasswordLength, MinimumLength = AuthSettings.MinPasswordLength,
        ErrorMessage = "Password harus 8–128 karakter.")]
    [RegularExpression(AuthSettings.PasswordPattern, ErrorMessage = "Password harus mengandung huruf dan angka.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ulangi password wajib diisi.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Password gak sama.")]
    [Display(Name = "Ulangi Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
