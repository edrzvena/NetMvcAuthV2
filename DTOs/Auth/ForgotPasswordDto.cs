using System.ComponentModel.DataAnnotations;

namespace NetMvcAuthV2.DTOs.Auth;

public class ForgotPasswordDto
{
    [Required(ErrorMessage = "Email wajib diisi.")]
    [EmailAddress(ErrorMessage = "Format email tidak valid.")]
    public string Email { get; set; } = string.Empty;
}
