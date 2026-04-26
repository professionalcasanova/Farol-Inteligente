using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Auth;

public sealed class LogoutRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
