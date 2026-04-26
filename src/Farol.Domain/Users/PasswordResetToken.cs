namespace Farol.Domain.Users;

public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
        Token = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Token { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public bool Used { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public PasswordResetToken(Guid userId, string token, DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Password reset token must belong to a user.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("Password reset token is required.", nameof(token));
        }

        if (expiresAtUtc == default)
        {
            throw new ArgumentException("Password reset token expiration is required.", nameof(expiresAtUtc));
        }

        Id = Guid.NewGuid();
        UserId = userId;
        Token = token.Trim();
        ExpiresAtUtc = expiresAtUtc;
        Used = false;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool IsExpired(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        return ExpiresAtUtc <= timeProvider.GetUtcNow();
    }

    public void MarkAsUsed()
    {
        Used = true;
    }
}
