using Farol.Domain.Bills;

namespace Farol.Tests.Domain;

public sealed class BillTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_ShouldRejectAmountLessThanOrEqualToZero(decimal amount)
    {
        var action = () => new Bill(Guid.NewGuid(), "Internet", amount, new DateOnly(2026, 3, 25));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void MarkAsPaid_ShouldSetPaymentFlags()
    {
        var paidAtUtc = new DateTimeOffset(2026, 3, 17, 12, 30, 0, TimeSpan.Zero);
        var bill = new Bill(Guid.NewGuid(), "Internet", 99.90m, new DateOnly(2026, 3, 25));

        bill.MarkAsPaid(paidAtUtc);

        Assert.True(bill.IsPaid);
        Assert.Equal(paidAtUtc, bill.PaidAtUtc);
    }

    [Fact]
    public void MarkAsUnpaid_ShouldClearPaymentFlags()
    {
        var bill = new Bill(Guid.NewGuid(), "Internet", 99.90m, new DateOnly(2026, 3, 25));
        bill.MarkAsPaid(new DateTimeOffset(2026, 3, 17, 12, 30, 0, TimeSpan.Zero));

        bill.MarkAsUnpaid();

        Assert.False(bill.IsPaid);
        Assert.Null(bill.PaidAtUtc);
    }
}
