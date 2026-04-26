using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class ForgotPasswordRequest
{
    [Required]
    public string Email { get; init; } = string.Empty;
}
