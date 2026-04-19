namespace Farol.Domain.Bills;

public sealed class Bill
{
    private const int DescriptionMaxLength = 255;

    private Bill()
    {
        Description = string.Empty;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Description { get; private set; }
    public decimal Amount { get; private set; }
    public DateOnly DueOn { get; private set; }
    public Guid? BillSeriesId { get; private set; }
    public int? OccurrenceNumber { get; private set; }
    public int? TotalOccurrences { get; private set; }
    public bool IsPaid { get; private set; }
    public Guid? PaidTransactionId { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Bill(Guid userId, string description, decimal amount, DateOnly dueOn)
        : this(userId, description, amount, dueOn, null, null, null)
    {
    }

    internal Bill(
        Guid userId,
        string description,
        decimal amount,
        DateOnly dueOn,
        Guid? billSeriesId,
        int? occurrenceNumber,
        int? totalOccurrences)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        Description = NormalizeDescription(description);
        Amount = EnsureAmount(amount);
        DueOn = EnsureDueOn(dueOn);
        BillSeriesId = EnsureBillSeriesId(billSeriesId, occurrenceNumber, totalOccurrences);
        OccurrenceNumber = EnsureOccurrenceNumber(billSeriesId, occurrenceNumber);
        TotalOccurrences = EnsureTotalOccurrences(billSeriesId, occurrenceNumber, totalOccurrences);
        IsPaid = false;
        PaidTransactionId = null;
        PaidAtUtc = null;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAsPaid(DateTimeOffset? paidAtUtc = null, Guid? paidTransactionId = null)
    {
        if (IsPaid && PaidAtUtc.HasValue)
        {
            return;
        }

        IsPaid = true;
        PaidTransactionId = EnsurePaidTransactionId(paidTransactionId);
        PaidAtUtc = paidAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
    }

    public void MarkAsUnpaid()
    {
        IsPaid = false;
        PaidTransactionId = null;
        PaidAtUtc = null;
    }

    public void UpdateDetails(string description, decimal amount, DateOnly dueOn)
    {
        Description = NormalizeDescription(description);
        Amount = EnsureAmount(amount);
        DueOn = EnsureDueOn(dueOn);
    }

    public void ReassignSeries(Guid billSeriesId, int? occurrenceNumber, int? totalOccurrences)
    {
        BillSeriesId = EnsureBillSeriesId(billSeriesId, occurrenceNumber, totalOccurrences);
        OccurrenceNumber = EnsureOccurrenceNumber(billSeriesId, occurrenceNumber);
        TotalOccurrences = EnsureTotalOccurrences(billSeriesId, occurrenceNumber, totalOccurrences);
    }

    private static Guid EnsureUserId(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Bill must belong to a user.", nameof(userId));
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

    private static Guid? EnsureBillSeriesId(Guid? billSeriesId, int? occurrenceNumber, int? totalOccurrences)
    {
        if (!billSeriesId.HasValue)
        {
            if (occurrenceNumber.HasValue || totalOccurrences.HasValue)
            {
                throw new ArgumentException(
                    "Bill occurrence metadata requires a recurring bill series.",
                    nameof(billSeriesId));
            }

            return null;
        }

        if (billSeriesId == Guid.Empty)
        {
            throw new ArgumentException("Recurring bill series is invalid.", nameof(billSeriesId));
        }

        return billSeriesId.Value;
    }

    private static int? EnsureOccurrenceNumber(Guid? billSeriesId, int? occurrenceNumber)
    {
        if (!billSeriesId.HasValue)
        {
            return null;
        }

        if (!occurrenceNumber.HasValue || occurrenceNumber.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(occurrenceNumber),
                "Recurring bill occurrence number must be greater than zero.");
        }

        return occurrenceNumber.Value;
    }

    private static int? EnsureTotalOccurrences(Guid? billSeriesId, int? occurrenceNumber, int? totalOccurrences)
    {
        if (!billSeriesId.HasValue)
        {
            return null;
        }

        if (!totalOccurrences.HasValue)
        {
            return null;
        }

        if (totalOccurrences.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(totalOccurrences),
                "Recurring bill total occurrences must be greater than zero.");
        }

        if (!occurrenceNumber.HasValue || totalOccurrences.Value < occurrenceNumber.Value)
        {
            throw new ArgumentException(
                "Recurring bill total occurrences cannot be lower than the current occurrence number.",
                nameof(totalOccurrences));
        }

        return totalOccurrences.Value;
    }

    private static DateOnly EnsureDueOn(DateOnly value)
    {
        if (value == default)
        {
            throw new ArgumentException("Bill due date is required.", nameof(value));
        }

        return value;
    }

    private static Guid? EnsurePaidTransactionId(Guid? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value == Guid.Empty)
        {
            throw new ArgumentException("Paid bill transaction is invalid.", nameof(value));
        }

        return value.Value;
    }
}
