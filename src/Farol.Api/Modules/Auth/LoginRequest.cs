using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class LoginRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
