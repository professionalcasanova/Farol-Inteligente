namespace Farol.Domain.Budgets;

public sealed class CommunityBudget
{
    private const int TitleMaxLength = 120;
    private const int DescriptionMaxLength = 500;
    private const int TargetProfileMaxLength = 120;

    private CommunityBudget()
    {
        Title = string.Empty;
        Description = string.Empty;
        TargetProfile = string.Empty;
        Status = CommunityBudgetStatus.Draft;
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string TargetProfile { get; private set; }
    public decimal? MonthlyIncomeReference { get; private set; }
    public bool IsPublic { get; private set; }
    public CommunityBudgetStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public CommunityBudget(
        Guid ownerUserId,
        string title,
        string description,
        string targetProfile,
        decimal? monthlyIncomeReference,
        CommunityBudgetStatus status)
    {
        var now = DateTimeOffset.UtcNow;

        Id = Guid.NewGuid();
        OwnerUserId = EnsureOwnerUserId(ownerUserId);
        Title = EnsureText(title, nameof(title), "Community budget title", TitleMaxLength);
        Description = EnsureText(description, nameof(description), "Community budget description", DescriptionMaxLength);
        TargetProfile = EnsureText(targetProfile, nameof(targetProfile), "Community budget target profile", TargetProfileMaxLength);
        MonthlyIncomeReference = EnsureOptionalMoney(monthlyIncomeReference, nameof(monthlyIncomeReference));
        Status = EnsureUserManagedStatus(status, nameof(status));
        IsPublic = Status == CommunityBudgetStatus.Published;
        CreatedAtUtc = now;
        UpdatedAtUtc = now;
    }

    public void UpdateDetails(
        string title,
        string description,
        string targetProfile,
        decimal? monthlyIncomeReference,
        CommunityBudgetStatus status)
    {
        if (!CanBeEditedByOwner())
        {
            throw new InvalidOperationException("Community budget cannot be edited in the current status.");
        }

        Title = EnsureText(title, nameof(title), "Community budget title", TitleMaxLength);
        Description = EnsureText(description, nameof(description), "Community budget description", DescriptionMaxLength);
        TargetProfile = EnsureText(targetProfile, nameof(targetProfile), "Community budget target profile", TargetProfileMaxLength);
        MonthlyIncomeReference = EnsureOptionalMoney(monthlyIncomeReference, nameof(monthlyIncomeReference));
        Status = EnsureUserManagedStatus(status, nameof(status));
        IsPublic = Status == CommunityBudgetStatus.Published;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    public bool CanBeEditedByOwner()
    {
        return Status is CommunityBudgetStatus.Draft or CommunityBudgetStatus.Published;
    }

    public void MarkReported()
    {
        Status = CommunityBudgetStatus.Reported;
        IsPublic = false;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static Guid EnsureOwnerUserId(Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("Community budget must belong to a user.", nameof(ownerUserId));
        }

        return ownerUserId;
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

    private static decimal? EnsureOptionalMoney(decimal? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Monthly income reference must be greater than zero.");
        }

        return decimal.Round(value.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static CommunityBudgetStatus EnsureUserManagedStatus(
        CommunityBudgetStatus status,
        string parameterName)
    {
        if (status is not CommunityBudgetStatus.Draft and not CommunityBudgetStatus.Published)
        {
            throw new ArgumentException("Community budget status can only be draft or published in user-managed flows.", parameterName);
        }

        return status;
    }
}
