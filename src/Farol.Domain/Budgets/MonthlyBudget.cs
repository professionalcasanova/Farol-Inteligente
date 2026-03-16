namespace Farol.Domain.Budgets;

public sealed class MonthlyBudget
{
    private MonthlyBudget()
    {
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int Month { get; private set; }
    public int Year { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public MonthlyBudget(Guid userId, int month, int year)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        Month = EnsureMonth(month);
        Year = EnsureYear(year);
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Monthly budget must belong to a user.", nameof(userId));
        }

        return userId;
    }

    private static int EnsureMonth(int month)
    {
        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Monthly budget month must be between 1 and 12.");
        }

        return month;
    }

    private static int EnsureYear(int year)
    {
        if (year is < 2000 or > 2100)
        {
            throw new ArgumentOutOfRangeException(nameof(year), "Monthly budget year is invalid.");
        }

        return year;
    }
}
