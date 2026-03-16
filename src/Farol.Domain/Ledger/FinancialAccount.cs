namespace Farol.Domain.Ledger;

public sealed class FinancialAccount
{
    private const int NameMaxLength = 120;

    private FinancialAccount()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; }
    public FinancialAccountType Type { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public FinancialAccount(Guid userId, string name, FinancialAccountType type)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        Name = NormalizeName(name);
        Type = EnsureType(type);
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string name, FinancialAccountType type)
    {
        Name = NormalizeName(name);
        Type = EnsureType(type);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Financial account must belong to a user.", nameof(userId));
        }

        return userId;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Financial account name is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException(
                $"Financial account name cannot exceed {NameMaxLength} characters.",
                nameof(value));
        }

        return normalized;
    }

    private static FinancialAccountType EnsureType(FinancialAccountType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Financial account type is invalid.");
        }

        return value;
    }
}
