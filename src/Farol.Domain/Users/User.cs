namespace Farol.Domain.Users;

public sealed class User
{
    private const int NameMaxLength = 120;
    private const int EmailMaxLength = 254;

    private User()
    {
        Name = string.Empty;
        Email = string.Empty;
        PasswordHash = string.Empty;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public User(string name, string email, string passwordHash)
    {
        Id = Guid.NewGuid();
        Name = NormalizeName(name);
        Email = NormalizeEmail(email);
        PasswordHash = NormalizePasswordHash(passwordHash);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateProfile(string name, string email)
    {
        Name = NormalizeName(name);
        Email = NormalizeEmail(email);
    }

    public void ChangePasswordHash(string passwordHash)
    {
        PasswordHash = NormalizePasswordHash(passwordHash);
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User name is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"User name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("User email is required.", nameof(value));
        }

        var normalized = value.Trim().ToLowerInvariant();

        if (normalized.Length > EmailMaxLength)
        {
            throw new ArgumentException($"User email cannot exceed {EmailMaxLength} characters.", nameof(value));
        }

        // Full email validation will be enforced by the application layer and/or database constraints.
        if (!normalized.Contains('@'))
        {
            throw new ArgumentException("User email must be valid.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizePasswordHash(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Password hash is required.", nameof(value));
        }

        return value.Trim();
    }
}
