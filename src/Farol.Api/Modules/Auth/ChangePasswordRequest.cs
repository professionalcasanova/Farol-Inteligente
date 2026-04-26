using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    public string NewPassword { get; init; } = string.Empty;

    [Required]
    public string ConfirmNewPassword { get; init; } = string.Empty;
}
