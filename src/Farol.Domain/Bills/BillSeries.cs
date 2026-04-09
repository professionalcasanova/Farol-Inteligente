namespace Farol.Domain.Bills;

public sealed class BillSeries
{
    public const string MonthlyFrequency = "monthly";
    public const string OpenEndedEndMode = "open_ended";
    public const string UntilDateEndMode = "until_date";
    public const string OccurrenceCountEndMode = "occurrence_count";
    public const string RecurringKind = "recurring";
    public const string InstallmentKind = "installment";

    private const int DescriptionMaxLength = 255;

    private BillSeries()
    {
        Description = string.Empty;
        Kind = RecurringKind;
        Frequency = MonthlyFrequency;
        EndMode = OpenEndedEndMode;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly FirstDueOn { get; private set; }
    public string Kind { get; private set; }
    public string Frequency { get; private set; }
    public string EndMode { get; private set; }
    public DateOnly? UntilDate { get; private set; }
    public int? OccurrenceCount { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public BillSeries(
        Guid userId,
        string description,
        decimal amount,
        DateOnly firstDueOn,
        string kind,
        string frequency,
        string endMode,
        DateOnly? untilDate,
        int? occurrenceCount)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        Description = NormalizeDescription(description);
        Amount = EnsureAmount(amount);
        FirstDueOn = EnsureDueOn(firstDueOn);
        Kind = EnsureKind(kind);
        Frequency = EnsureFrequency(frequency);
        EndMode = EnsureEndMode(endMode);
        UntilDate = EnsureUntilDate(FirstDueOn, EndMode, untilDate);
        OccurrenceCount = EnsureOccurrenceCount(EndMode, occurrenceCount);
        EnsureKindMatchesSchedule(Kind, EndMode, OccurrenceCount);
        IsActive = true;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Bill CreateFirstOccurrence()
    {
        return CreateOccurrenceForMonth(new DateOnly(FirstDueOn.Year, FirstDueOn.Month, 1));
    }

    public Bill CreateOccurrenceForMonth(DateOnly periodStart)
    {
        if (!TryResolveOccurrenceForMonth(periodStart, out var dueOn, out var occurrenceNumber, out var totalOccurrences))
        {
            throw new InvalidOperationException("Bill series does not produce an occurrence for the requested month.");
        }

        return new Bill(UserId, Description, Amount, dueOn, Id, occurrenceNumber, totalOccurrences);
    }

    public bool TryResolveOccurrenceForMonth(
        DateOnly periodStart,
        out DateOnly dueOn,
        out int occurrenceNumber,
        out int? totalOccurrences)
    {
        dueOn = default;
        occurrenceNumber = 0;
        totalOccurrences = null;

        if (!IsActive)
        {
            return false;
        }

        if (periodStart.Day != 1)
        {
            throw new ArgumentException("Recurring bill expansion expects the first day of the month.", nameof(periodStart));
        }

        var firstPeriodStart = new DateOnly(FirstDueOn.Year, FirstDueOn.Month, 1);
        var monthOffset = ((periodStart.Year - firstPeriodStart.Year) * 12) + (periodStart.Month - firstPeriodStart.Month);

        if (monthOffset < 0)
        {
            return false;
        }

        occurrenceNumber = monthOffset + 1;

        if (OccurrenceCount.HasValue && occurrenceNumber > OccurrenceCount.Value)
        {
            return false;
        }

        dueOn = ResolveDueOn(periodStart);

        if (UntilDate.HasValue && dueOn > UntilDate.Value)
        {
            return false;
        }

        totalOccurrences = ResolveTotalOccurrences();
        return true;
    }

    private int? ResolveTotalOccurrences()
    {
        if (OccurrenceCount.HasValue)
        {
            return OccurrenceCount.Value;
        }

        if (!UntilDate.HasValue)
        {
            return null;
        }

        var total = 0;
        var period = new DateOnly(FirstDueOn.Year, FirstDueOn.Month, 1);

        while (true)
        {
            var dueOn = ResolveDueOn(period);

            if (dueOn > UntilDate.Value)
            {
                break;
            }

            total += 1;
            period = period.AddMonths(1);
        }

        return total;
    }

    private DateOnly ResolveDueOn(DateOnly periodStart)
    {
        var day = Math.Min(FirstDueOn.Day, DateTime.DaysInMonth(periodStart.Year, periodStart.Month));
        return new DateOnly(periodStart.Year, periodStart.Month, day);
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Recurring bill series must belong to a user.", nameof(userId));
        }

        return userId;
    }

    private static string NormalizeDescription(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Bill description is required.", nameof(value));
        }

        var normalized = value.Trim();

        if (normalized.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"Bill description cannot exceed {DescriptionMaxLength} characters.",
                nameof(value));
        }

        return normalized;
    }

    private static decimal EnsureAmount(decimal value)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Bill amount must be greater than zero.");
        }

        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }

    private static DateOnly EnsureDueOn(DateOnly value)
    {
        if (value == default)
        {
            throw new ArgumentException("Bill due date is required.", nameof(value));
        }

        return value;
    }

    private static string EnsureFrequency(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (normalized != MonthlyFrequency)
        {
            throw new ArgumentException("Recurring bill frequency is invalid. Use monthly.", nameof(value));
        }

        return normalized;
    }

    private static string EnsureKind(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (normalized is not (RecurringKind or InstallmentKind))
        {
            throw new ArgumentException(
                "Recurring bill kind is invalid. Use recurring or installment.",
                nameof(value));
        }

        return normalized;
    }

    private static string EnsureEndMode(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        if (normalized is not (OpenEndedEndMode or UntilDateEndMode or OccurrenceCountEndMode))
        {
            throw new ArgumentException(
                "Recurring bill end mode is invalid. Use open_ended, until_date or occurrence_count.",
                nameof(value));
        }

        return normalized;
    }

    private static DateOnly? EnsureUntilDate(DateOnly firstDueOn, string endMode, DateOnly? untilDate)
    {
        if (endMode == UntilDateEndMode)
        {
            if (!untilDate.HasValue)
            {
                throw new ArgumentException(
                    "Recurring bill end date is required when end mode is until_date.",
                    nameof(untilDate));
            }

            if (untilDate.Value < firstDueOn)
            {
                throw new ArgumentException(
                    "Recurring bill end date must be on or after the first due date.",
                    nameof(untilDate));
            }

            return untilDate.Value;
        }

        if (untilDate.HasValue)
        {
            throw new ArgumentException(
                "Recurring bill end date must only be provided when end mode is until_date.",
                nameof(untilDate));
        }

        return null;
    }

    private static int? EnsureOccurrenceCount(string endMode, int? occurrenceCount)
    {
        if (endMode == OccurrenceCountEndMode)
        {
            if (!occurrenceCount.HasValue)
            {
                throw new ArgumentException(
                    "Recurring bill occurrence count is required when end mode is occurrence_count.",
                    nameof(occurrenceCount));
            }

            if (occurrenceCount.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(occurrenceCount),
                    "Recurring bill occurrence count must be greater than zero.");
            }

            return occurrenceCount.Value;
        }

        if (occurrenceCount.HasValue)
        {
            throw new ArgumentException(
                "Recurring bill occurrence count must only be provided when end mode is occurrence_count.",
                nameof(occurrenceCount));
        }

        return null;
    }

    private static void EnsureKindMatchesSchedule(string kind, string endMode, int? occurrenceCount)
    {
        if (kind != InstallmentKind)
        {
            return;
        }

        if (endMode != OccurrenceCountEndMode)
        {
            throw new ArgumentException("Installment bills must use occurrence_count end mode.");
        }

        if (!occurrenceCount.HasValue || occurrenceCount.Value < 2)
        {
            throw new ArgumentException("Installment count must be at least 2.");
        }
    }
}
