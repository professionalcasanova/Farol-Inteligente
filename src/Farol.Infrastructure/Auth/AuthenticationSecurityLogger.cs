using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Farol.Infrastructure.Auth;

internal static class AuthenticationSecurityLogger
{
    private const string AuthenticationContext = "Authentication";
    private const string PasswordResetRequestedEvent = "PasswordResetRequested";
    private const string PasswordResetCompletedEvent = "PasswordResetCompleted";

    public static void LogPasswordResetRequested(
        ILogger logger,
        Guid userId,
        DateTimeOffset timestampUtc)
    {
        LogAuthenticationEvent(logger, PasswordResetRequestedEvent, userId, timestampUtc);
    }

    public static void LogPasswordResetCompleted(
        ILogger logger,
        Guid userId,
        DateTimeOffset timestampUtc)
    {
        LogAuthenticationEvent(logger, PasswordResetCompletedEvent, userId, timestampUtc);
    }

    private static void LogAuthenticationEvent(
        ILogger logger,
        string eventType,
        Guid userId,
        DateTimeOffset timestampUtc)
    {
        logger.LogInformation(
            "Authentication event {EventType} for user {UserId} at {TimestampUtc} in context {Context}.",
            eventType,
            userId,
            timestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            AuthenticationContext);
    }
}
