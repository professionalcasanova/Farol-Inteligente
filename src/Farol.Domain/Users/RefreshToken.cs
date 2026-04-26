namespace Farol.Domain.Users;

public sealed class RefreshToken
{
    private RefreshToken()
    {
        Token = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool Revoked { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public RefreshToken(
        Guid userId,
        string token,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Refresh token must belong to a user.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Refresh token is required.", nameof(token));
        }

        if (createdAtUtc == default)
        {
            throw new ArgumentException("Refresh token creation time is required.", nameof(createdAtUtc));
        }

        if (expiresAtUtc == default || expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Refresh token expiration must be after creation.", nameof(expiresAtUtc));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Token = token.Trim();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Revoked = false;
    }

    public bool IsExpired(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return ExpiresAtUtc <= timeProvider.GetUtcNow();
    }

    public void Revoke()
    {
        Revoked = true;
    }
}
