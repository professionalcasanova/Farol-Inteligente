using System.Security.Cryptography;
using Farol.Domain.Users;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Farol.Infrastructure.Auth;

public sealed class PasswordResetService(
    FarolDbContext dbContext,
    PasswordService passwordService,
    TimeProvider timeProvider,
    ILogger<PasswordResetService> logger)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            return;
        }

        var activeTokens = await dbContext.PasswordResetTokens
            .Where(token => token.UserId == user.Id && !token.Used && token.ExpiresAtUtc > timeProvider.GetUtcNow())
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.MarkAsUsed();
        }

        var resetToken = new PasswordResetToken(
            user.Id,
            GenerateToken(),
            timeProvider.GetUtcNow().Add(TokenLifetime));

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        AuthenticationSecurityLogger.LogPasswordResetRequested(
            logger,
            user.Id,
            timeProvider.GetUtcNow());
    }

    public async Task<PasswordResetResult> ResetPasswordAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
        {
            return PasswordResetResult.InvalidToken;
        }

        var resetToken = await dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(candidate => candidate.Token == token.Trim(), cancellationToken);

        if (resetToken is null || resetToken.Used || resetToken.IsExpired(timeProvider))
        {
            return PasswordResetResult.InvalidToken;
        }

        var passwordValidation = passwordService.ValidatePassword(newPassword);

        if (!passwordValidation.IsValid)
        {
            return PasswordResetResult.WeakPassword;
        }

        var user = await dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == resetToken.UserId, cancellationToken);

        if (user is null)
        {
            return PasswordResetResult.InvalidToken;
        }

        var passwordHash = passwordService.HashPassword(user, newPassword);
        user.ChangePasswordHash(passwordHash);
        resetToken.MarkAsUsed();

        var siblingActiveTokens = await dbContext.PasswordResetTokens
            .Where(candidate => candidate.UserId == user.Id && candidate.Id != resetToken.Id && !candidate.Used)
            .ToListAsync(cancellationToken);

        foreach (var siblingToken in siblingActiveTokens)
        {
            siblingToken.MarkAsUsed();
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        AuthenticationSecurityLogger.LogPasswordResetCompleted(
            logger,
            user.Id,
            timeProvider.GetUtcNow());

        return PasswordResetResult.Success;
    }

    private static string NormalizeEmail(string email)
    {
        return email.Trim().ToLowerInvariant();
    }

    private static string GenerateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }
}

public enum PasswordResetResult
{
    Success = 0,
    InvalidToken = 1,
    WeakPassword = 2
}
