using System.Security.Cryptography;
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
        var refreshToken = new RefreshToken(
            user.Id,
            GenerateToken(),
            now,
            now.Add(RefreshTokenLifetime));

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthSession(
            jwtTokenService.CreateAccessToken(user),
            refreshToken.Token);
    }

    public async Task<RefreshSessionResult> RefreshAsync(
        string refreshTokenValue,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return RefreshSessionResult.Invalid();
        }

        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.Token == refreshTokenValue.Trim(), cancellationToken);

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
        var rotatedRefreshToken = new RefreshToken(
            user.Id,
            GenerateToken(),
            now,
            now.Add(RefreshTokenLifetime));

        dbContext.RefreshTokens.Add(rotatedRefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return RefreshSessionResult.Success(
            user,
            jwtTokenService.CreateAccessToken(user),
            rotatedRefreshToken.Token);
    }

    public async Task RevokeAsync(string refreshTokenValue, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenValue))
        {
            return;
        }

        var refreshToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.Token == refreshTokenValue.Trim(), cancellationToken);

        if (refreshToken is null || refreshToken.Revoked)
        {
            return;
        }

        refreshToken.Revoke();
        await dbContext.SaveChangesAsync(cancellationToken);
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
