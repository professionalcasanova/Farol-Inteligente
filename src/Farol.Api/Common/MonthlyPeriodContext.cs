namespace Farol.Api.Common;

public sealed record MonthlyPeriodContext(
    DateOnly Start,
    DateOnly End,
    DateOnly Today,
    DateOnly CurrentMonthStart)
{
    private const string PendingStatus = "pending";
    private const string PaidStatus = "paid";
    private const string OverdueStatus = "overdue";

    public bool IsFuturePeriod => Start > CurrentMonthStart;

    public bool IsPendingBill(bool isPaid, DateOnly dueOn)
    {
        return !isPaid && (IsFuturePeriod || dueOn >= Today);
    }

    public bool IsOverdueBill(bool isPaid, DateOnly dueOn)
    {
        return !isPaid && !IsFuturePeriod && dueOn < Today;
    }

    public string ResolveBillStatus(bool isPaid, DateOnly dueOn)
    {
        if (isPaid)
        {
            return PaidStatus;
        }

        return dueOn < Today && !IsFuturePeriod ? OverdueStatus : PendingStatus;
    }

    public static bool TryCreate(
        int month,
        int year,
        TimeProvider timeProvider,
        out MonthlyPeriodContext? context)
    {
        try
        {
            var start = new DateOnly(year, month, 1);
            var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
            var currentMonthStart = new DateOnly(today.Year, today.Month, 1);

            context = new MonthlyPeriodContext(
                start,
                start.AddMonths(1),
                today,
                currentMonthStart);

            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            context = null;
            return false;
        }
    }
}
