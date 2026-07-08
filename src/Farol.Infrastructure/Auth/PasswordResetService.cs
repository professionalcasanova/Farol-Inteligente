using System.Security.Cryptography;
using Farol.Domain.Users;
using Farol.Infrastructure.Email;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Farol.Infrastructure.Auth;

public sealed class PasswordResetService(
    FarolDbContext dbContext,
    PasswordService passwordService,
    TimeProvider timeProvider,
    ILogger<PasswordResetService> logger,
    IEmailService emailService,
    PasswordResetEmailTemplate passwordResetEmailTemplate,
    IOptions<EmailOptions> emailOptions)
{
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

        var tokenForEmail = GenerateToken();
        var resetToken = new PasswordResetToken(
            user.Id,
            HashToken(tokenForEmail),
            timeProvider.GetUtcNow().Add(TimeSpan.FromMinutes(emailOptions.Value.ResetTokenMinutes)));

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var emailResult = await emailService.SendAsync(
            passwordResetEmailTemplate.Build(user.Email, user.Name, tokenForEmail),
            cancellationToken);

        if (!emailResult.Succeeded)
        {
            logger.LogWarning(
                "TransactionalEmailFailed Event={Event} UserId={UserId} TimestampUtc={TimestampUtc:o} ErrorCode={ErrorCode}",
                "PasswordResetEmailFailed",
                user.Id,
                timeProvider.GetUtcNow(),
                emailResult.ErrorCode);
        }

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

        var tokenHash = HashToken(token.Trim());
        var resetToken = await dbContext.PasswordResetTokens
            .SingleOrDefaultAsync(candidate => candidate.Token == tokenHash, cancellationToken);

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

        var activeSessions = await dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == user.Id && !candidate.Revoked)
            .ToListAsync(cancellationToken);

        foreach (var activeSession in activeSessions)
        {
            activeSession.Revoke();
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

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
    }
}

public enum PasswordResetResult
{
    Success = 0,
    InvalidToken = 1,
    WeakPassword = 2
}
