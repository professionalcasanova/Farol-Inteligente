using Farol.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Farol.Infrastructure.Auth;

public sealed class PasswordService
{
    public const string PasswordPolicyErrorMessage =
        "Password must be at least 8 characters long, contain at least 1 letter and 1 number, and cannot be only spaces.";

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

    public PasswordValidationResult ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return PasswordValidationResult.Invalid(PasswordPolicyErrorMessage);
        }

        if (password.Length < 8)
        {
            return PasswordValidationResult.Invalid(PasswordPolicyErrorMessage);
        }

        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            return PasswordValidationResult.Invalid(PasswordPolicyErrorMessage);
        }

        return PasswordValidationResult.Valid();
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

public readonly record struct PasswordValidationResult(bool IsValid, string? ErrorMessage)
{
    public static PasswordValidationResult Valid() => new(true, null);
    public static PasswordValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}
