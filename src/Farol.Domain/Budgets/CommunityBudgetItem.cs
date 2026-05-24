namespace Farol.Domain.Budgets;

public sealed class CommunityBudgetItem
{
    private const int NameMaxLength = 120;
    private const int CategoryNameMaxLength = 120;
    private const int NotesMaxLength = 500;

    private CommunityBudgetItem()
    {
        Name = string.Empty;
        CategoryName = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid CommunityBudgetId { get; private set; }
    public string Name { get; private set; }
    public string CategoryName { get; private set; }
    public CommunityBudgetItemType Type { get; private set; }
    public CommunityBudgetAllocationType AllocationType { get; private set; }
    public decimal? Amount { get; private set; }
    public decimal? Percentage { get; private set; }
    public string? Notes { get; private set; }
    public int SortOrder { get; private set; }

    public CommunityBudgetItem(
        Guid communityBudgetId,
        string name,
        string categoryName,
        CommunityBudgetItemType type,
        CommunityBudgetAllocationType allocationType,
        decimal? amount,
        decimal? percentage,
        string? notes,
        int sortOrder)
    {
        Id = Guid.NewGuid();
        CommunityBudgetId = EnsureCommunityBudgetId(communityBudgetId);
        Name = EnsureText(name, nameof(name), "Community budget item name", NameMaxLength);
        CategoryName = EnsureText(categoryName, nameof(categoryName), "Community budget item category name", CategoryNameMaxLength);
        Type = type;
        AllocationType = allocationType;
        Amount = EnsureAmount(allocationType, amount, percentage);
        Percentage = EnsurePercentage(allocationType, amount, percentage);
        Notes = EnsureOptionalText(notes, nameof(notes), "Community budget item notes", NotesMaxLength);
        SortOrder = EnsureSortOrder(sortOrder);
    }

    private static Guid EnsureCommunityBudgetId(Guid communityBudgetId)
    {
        if (communityBudgetId == Guid.Empty)
        {
            throw new ArgumentException("Community budget item must belong to a community budget.", nameof(communityBudgetId));
        }

        return communityBudgetId;
    }

    private static string EnsureText(string value, string parameterName, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{label} is required.", parameterName);
        }

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{label} cannot exceed {maxLength} characters.", parameterName);
        }

        SensitiveBudgetTextValidator.EnsureSafe(normalized, parameterName);

        return normalized;
    }

    private static string? EnsureOptionalText(string? value, string parameterName, string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return EnsureText(value, parameterName, label, maxLength);
    }

    private static decimal? EnsureAmount(
        CommunityBudgetAllocationType allocationType,
        decimal? amount,
        decimal? percentage)
    {
        if (allocationType != CommunityBudgetAllocationType.FixedAmount)
        {
            if (amount is not null)
            {
                throw new InvalidOperationException("Amount must be empty when allocation type is percentage.");
            }

            return null;
        }

        if (amount is null || amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (percentage is not null)
        {
            throw new InvalidOperationException("Percentage must be empty when allocation type is fixed_amount.");
        }

        return decimal.Round(amount.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? EnsurePercentage(
        CommunityBudgetAllocationType allocationType,
        decimal? amount,
        decimal? percentage)
    {
        if (allocationType != CommunityBudgetAllocationType.Percentage)
        {
            return null;
        }

        if (percentage is null || percentage <= 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be greater than zero and less than or equal to 100.");
        }

        if (amount is not null)
        {
            throw new InvalidOperationException("Amount must be empty when allocation type is percentage.");
        }

        return decimal.Round(percentage.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static int EnsureSortOrder(int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        return sortOrder;
    }
}
