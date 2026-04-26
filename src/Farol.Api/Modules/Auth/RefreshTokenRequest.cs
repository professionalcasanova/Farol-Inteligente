using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
