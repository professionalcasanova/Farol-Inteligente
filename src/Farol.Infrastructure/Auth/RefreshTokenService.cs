using System.Security.Cryptography;
using System.Text;
using Farol.Domain.Users;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Infrastructure.Auth;

public sealed class RefreshTokenService(
    FarolDbContext dbContext,
    JwtTokenService jwtTokenService,
    TimeProvider timeProvider)
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(7);

    public async Task<AuthSession> CreateSessionAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = timeProvider.GetUtcNow();
        var tokenValue = GenerateToken();
        var refreshToken = new RefreshToken(
            user.Id,
            HashToken(tokenValue),
            now,
            now.Add(RefreshTokenLifetime));

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            jwtTokenService.CreateAccessToken(user),
            tokenValue);
    }

    public async Task<RefreshSessionResult> RefreshAsync(
        string refreshTokenValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return RefreshSessionResult.Invalid();
        }

        var hashedToken = HashToken(refreshTokenValue.Trim());
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.Token == hashedToken,
                cancellationToken);

        if (refreshToken is null)
        {
            return RefreshSessionResult.Invalid();
        }

        if (refreshToken.Revoked)
        {
            await RevokeActiveTokensAsync(refreshToken.UserId, cancellationToken);
            return RefreshSessionResult.Invalid();
        }

        if (refreshToken.IsExpired(timeProvider))
        {
            refreshToken.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);
            return RefreshSessionResult.Invalid();
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == refreshToken.UserId, cancellationToken);

        if (user is null)
        {
            return RefreshSessionResult.Invalid();
        }

        refreshToken.Revoke();

        var now = timeProvider.GetUtcNow();
        var rotatedTokenValue = GenerateToken();
        var rotatedRefreshToken = new RefreshToken(
            user.Id,
            HashToken(rotatedTokenValue),
            now,
            now.Add(RefreshTokenLifetime));

        dbContext.RefreshTokens.Add(rotatedRefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RefreshSessionResult.Success(
            user,
            jwtTokenService.CreateAccessToken(user),
            rotatedTokenValue);
    }

    public async Task RevokeAsync(string refreshTokenValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return;
        }

        var hashedToken = HashToken(refreshTokenValue.Trim());
        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.Token == hashedToken,
                cancellationToken);

        if (refreshToken is null || refreshToken.Revoked)
        {
            return;
        }

        refreshToken.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RefreshToken>> ListActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();

        return await dbContext.RefreshTokens
            .Where(token =>
                token.UserId == userId &&
                !token.Revoked &&
                token.ExpiresAtUtc > now)
            .OrderByDescending(token => token.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || sessionId == Guid.Empty)
        {
            return false;
        }

        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.Id == sessionId && token.UserId == userId,
                cancellationToken);

        if (refreshToken is null)
        {
            return false;
        }

        if (!refreshToken.Revoked)
        {
            refreshToken.Revoke();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        return RevokeActiveTokensAsync(userId, cancellationToken);
    }

    private async Task RevokeActiveTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && !token.Revoked)
            .ToListAsync(cancellationToken);

        if (activeTokens.Count == 0)
        {
            return;
        }

        foreach (var token in activeTokens)
        {
            token.Revoke();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}

public sealed record AuthSession(string AccessToken, string RefreshToken);

public sealed record RefreshSessionResult(
    bool IsSuccess,
    User? User,
    string? AccessToken,
    string? RefreshToken)
{
    public static RefreshSessionResult Invalid() => new(false, null, null, null);

    public static RefreshSessionResult Success(User user, string accessToken, string refreshToken) =>
        new(true, user, accessToken, refreshToken);
}
