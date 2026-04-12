using Farol.Domain.Ledger;

namespace Farol.Domain.Categories;

public sealed class Category
{
    private const int NameMaxLength = 100;

    private Category()
    {
        Name = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Name { get; private set; }
    public CategoryType Type { get; private set; }
    public bool IsSystem { get; private set; }

    private Category(Guid? userId, string name, CategoryType type, bool isSystem)
    {
        Id = Guid.NewGuid();
        UserId = ValidateOwnership(userId, isSystem);
        Name = NormalizeName(name);
        Type = EnsureType(type);
        IsSystem = isSystem;
    }

    public static Category CreateSystem(string name, CategoryType type)
    {
        return new Category(null, name, type, true);
    }

    public static Category CreateUserOwned(Guid userId, string name, CategoryType type)
    {
        return new Category(userId, name, type, false);
    }

    public void Rename(string name)
    {
        if (IsSystem)
        {
            throw new InvalidOperationException("System categories cannot be renamed.");
        }

        Name = NormalizeName(name);
    }

    public void CorrectSystemName(string name)
    {
        if (!IsSystem)
        {
            throw new InvalidOperationException("Only system categories can be corrected by seed maintenance.");
        }

        Name = NormalizeName(name);
    }

    public bool CanBeAssignedTo(TransactionType transactionType)
    {
        return Type == ToCategoryType(transactionType);
    }

    public void EnsureCanBeAssignedTo(TransactionType transactionType)
    {
        if (!CanBeAssignedTo(transactionType))
        {
            throw new InvalidOperationException("Category type is not compatible with the transaction type.");
        }
    }

    private static Guid? ValidateOwnership(Guid? userId, bool isSystem)
    {
        if (isSystem)
        {
            if (userId.HasValue)
            {
                throw new ArgumentException("System categories cannot have an owner.", nameof(userId));
            }

            return null;
        }

        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new ArgumentException("User-owned categories must belong to a user.", nameof(userId));
        }

        return userId.Value;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Category name is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > NameMaxLength)
        {
            throw new ArgumentException($"Category name cannot exceed {NameMaxLength} characters.", nameof(value));
        }

        return normalized;
    }

    private static CategoryType EnsureType(CategoryType value)
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Category type is invalid.");
        }

        return value;
    }

    private static CategoryType ToCategoryType(TransactionType transactionType)
    {
        return transactionType switch
        {
            TransactionType.Income => CategoryType.Income,
            TransactionType.Expense => CategoryType.Expense,
            _ => throw new ArgumentOutOfRangeException(nameof(transactionType), "Transaction type is invalid.")
        };
    }
}
