using Farol.Domain.Users;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Farol.Tests.Auth;

public sealed class PasswordResetServiceTests
{
    [Fact]
    public async Task PasswordResetFlow_ShouldWriteSafeAuthenticationLogsWithoutSensitiveData()
    {
        await using var dbContext = CreateDbContext();
        var passwordService = new PasswordService();
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(2026, 4, 29, 10, 30, 0, TimeSpan.Zero));
        var user = new User("Maria Silva", "maria@email.com", "temporary-hash");
        user.ChangePasswordHash(passwordService.HashPassword(user, "Password123"));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var logger = new CapturingLogger<PasswordResetService>();
        var service = new PasswordResetService(
            dbContext,
            passwordService,
            timeProvider,
            logger);

        await service.RequestPasswordResetAsync("maria@email.com", CancellationToken.None);

        var resetToken = await dbContext.PasswordResetTokens.SingleAsync();

        await service.ResetPasswordAsync(resetToken.Token, "Password456", CancellationToken.None);

        var loggedContent = string.Join(Environment.NewLine, logger.Messages);

        Assert.False(string.IsNullOrWhiteSpace(resetToken.Token));
        Assert.Contains("PasswordResetRequested", loggedContent, StringComparison.Ordinal);
        Assert.Contains("PasswordResetCompleted", loggedContent, StringComparison.Ordinal);
        Assert.Contains(user.Id.ToString(), loggedContent, StringComparison.Ordinal);
        Assert.Contains("2026-04-29T10:30:00.0000000+00:00", loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain(resetToken.Token, loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Password123", loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Password456", loggedContent, StringComparison.Ordinal);
        Assert.DoesNotContain("maria@email.com", loggedContent, StringComparison.OrdinalIgnoreCase);
    }

    private static FarolDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<FarolDbContext>()
            .UseInMemoryDatabase($"PasswordResetServiceTests-{Guid.NewGuid()}")
            .Options;

        return new FarolDbContext(options);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
