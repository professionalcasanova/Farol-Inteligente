using Farol.Domain.Users;

namespace Farol.Api.Modules.Auth;

public sealed class SessionResponse
{
    public Guid Id { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool Revoked { get; init; }

    public static SessionResponse FromRefreshToken(RefreshToken refreshToken)
    {
        return new SessionResponse
        {
            Id = refreshToken.Id,
            CreatedAt = refreshToken.CreatedAtUtc,
            ExpiresAt = refreshToken.ExpiresAtUtc,
            Revoked = refreshToken.Revoked
        };
    }
}
