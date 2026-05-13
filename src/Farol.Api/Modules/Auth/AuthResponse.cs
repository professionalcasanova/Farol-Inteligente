namespace Farol.Api.Modules.Auth;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }
    public required Guid UserId { get; init; }
    public required string Name { get; init; }
    public required string Email { get; init; }
}
