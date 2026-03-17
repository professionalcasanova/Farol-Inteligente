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
    public bool IsPaid { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Bill(Guid userId, string description, decimal amount, DateOnly dueOn)
    {
        Id = Guid.NewGuid();
        UserId = EnsureUserId(userId);
        Description = NormalizeDescription(description);
        Amount = EnsureAmount(amount);
        DueOn = EnsureDueOn(dueOn);
        IsPaid = false;
        PaidAtUtc = null;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public void MarkAsPaid(DateTimeOffset? paidAtUtc = null)
    {
        if (IsPaid && PaidAtUtc.HasValue)
        {
            return;
        }

        IsPaid = true;
        PaidAtUtc = paidAtUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
    }

    public void MarkAsUnpaid()
    {
        IsPaid = false;
        PaidAtUtc = null;
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

    private static DateOnly EnsureDueOn(DateOnly value)
    {
        if (value == default)
        {
            throw new ArgumentException("Bill due date is required.", nameof(value));
        }

        return value;
    }
}
