using Farol.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Farol.Infrastructure.Auth;

public sealed class PasswordService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        return _passwordHasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
