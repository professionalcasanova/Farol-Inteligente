using Farol.Domain.Categories;

namespace Farol.Domain.Ledger;

public sealed class Transaction
{
    private const int DescriptionMaxLength = 255;

    private Transaction()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FinancialAccountId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateOnly OccurredOn { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Transaction(
        FinancialAccount financialAccount,
        TransactionType type,
        decimal amount,
        string description,
        DateOnly occurredOn,
        Category? category = null)
    {
        ArgumentNullException.ThrowIfNull(financialAccount);

        Id = Guid.NewGuid();
        UserId = EnsureUserId(financialAccount.UserId);
        CreatedAtUtc = DateTimeOffset.UtcNow;

        Update(financialAccount, type, amount, description, occurredOn);

        if (category is not null)
        {
            AssignCategory(category);
        }
    }

    public void Update(
        FinancialAccount financialAccount,
        TransactionType type,
        decimal amount,
        string description,
        DateOnly occurredOn)
    {
        ArgumentNullException.ThrowIfNull(financialAccount);

        EnsureAccountBelongsToUser(financialAccount);

        var newType = EnsureType(type);

        if (CategoryId.HasValue && newType != Type)
        {
            throw new InvalidOperationException(
                "Cannot change the type of a categorized transaction without removing its category first.");
        }

        FinancialAccountId = EnsureFinancialAccountId(financialAccount.Id);
        Type = newType;
        Amount = EnsureAmount(amount);
        Description = NormalizeDescription(description);
        OccurredOn = EnsureOccurredOn(occurredOn);
    }

    public void AssignCategory(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);

        if (!category.IsSystem && category.UserId != UserId)
        {
            throw new InvalidOperationException("Category must belong to the same user as the transaction.");
        }

        category.EnsureCanBeAssignedTo(Type);
        CategoryId = category.Id;
    }

    public void RemoveCategory()
    {
        CategoryId = null;
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Transaction must belong to a user.", nameof(userId));
        }

        return userId;
    }

    private void EnsureAccountBelongsToUser(FinancialAccount financialAccount)
    {
        if (financialAccount.Id == Guid.Empty)
        {
            throw new ArgumentException("Transaction must reference a valid financial account.", nameof(financialAccount));
        }

        if (financialAccount.UserId != UserId)
        {
            throw new InvalidOperationException("Financial account must belong to the same user as the transaction.");
        }
    }

    private static Guid EnsureFinancialAccountId(Guid financialAccountId)
    {
        if (financialAccountId == Guid.Empty)
        {
            throw new ArgumentException("Transaction must reference a financial account.", nameof(financialAccountId));
        }

        return financialAccountId;
    }

    private static TransactionType EnsureType(TransactionType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Transaction type is invalid.");
        }

        return value;
    }

    private static decimal EnsureAmount(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Transaction amount must be greater than zero.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static string NormalizeDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Transaction description is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Transaction description cannot exceed {DescriptionMaxLength} characters.",
                nameof(value));
        }

        return normalized;
    }

    private static DateOnly EnsureOccurredOn(DateOnly value)
    {
        if (value == default)
        {
            throw new ArgumentException("Transaction occurrence date is required.", nameof(value));
        }

        return value;
    }
}
