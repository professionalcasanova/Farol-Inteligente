using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class ResetPasswordRequest
{
    [Required]
    public string Token { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
