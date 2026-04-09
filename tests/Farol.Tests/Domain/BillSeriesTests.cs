using Farol.Domain.Bills;

namespace Farol.Tests.Domain;

public sealed class BillSeriesTests
{
    [Fact]
    public void CreateOccurrenceForMonth_ShouldClampDueDateToLastDayOfMonth()
    {
        var series = new BillSeries(
            Guid.NewGuid(),
            "Internet",
            99.90m,
            new DateOnly(2026, 1, 31),
            BillSeries.RecurringKind,
            BillSeries.MonthlyFrequency,
            BillSeries.OpenEndedEndMode,
            null,
            null);

        var bill = series.CreateOccurrenceForMonth(new DateOnly(2026, 2, 1));

        Assert.Equal(new DateOnly(2026, 2, 28), bill.DueOn);
        Assert.Equal(2, bill.OccurrenceNumber);
        Assert.Null(bill.TotalOccurrences);
    }

    [Fact]
    public void TryResolveOccurrenceForMonth_ShouldStopWhenOccurrenceCountIsReached()
    {
        var series = new BillSeries(
            Guid.NewGuid(),
            "Academia",
            120m,
            new DateOnly(2026, 3, 10),
            BillSeries.RecurringKind,
            BillSeries.MonthlyFrequency,
            BillSeries.OccurrenceCountEndMode,
            null,
            3);

        var hasMarch = series.TryResolveOccurrenceForMonth(
            new DateOnly(2026, 3, 1),
            out _,
            out var marchOccurrence,
            out var marchTotal);
        var hasMay = series.TryResolveOccurrenceForMonth(
            new DateOnly(2026, 5, 1),
            out _,
            out var mayOccurrence,
            out var mayTotal);
        var hasJune = series.TryResolveOccurrenceForMonth(
            new DateOnly(2026, 6, 1),
            out _,
            out _,
            out _);

        Assert.True(hasMarch);
        Assert.Equal(1, marchOccurrence);
        Assert.Equal(3, marchTotal);
        Assert.True(hasMay);
        Assert.Equal(3, mayOccurrence);
        Assert.Equal(3, mayTotal);
        Assert.False(hasJune);
    }

    [Fact]
    public void TryResolveOccurrenceForMonth_ShouldStopAfterUntilDate()
    {
        var series = new BillSeries(
            Guid.NewGuid(),
            "Escola",
            450m,
            new DateOnly(2026, 3, 15),
            BillSeries.RecurringKind,
            BillSeries.MonthlyFrequency,
            BillSeries.UntilDateEndMode,
            new DateOnly(2026, 5, 20),
            null);

        var hasMay = series.TryResolveOccurrenceForMonth(
            new DateOnly(2026, 5, 1),
            out var mayDueOn,
            out var mayOccurrence,
            out var mayTotal);
        var hasJune = series.TryResolveOccurrenceForMonth(
            new DateOnly(2026, 6, 1),
            out _,
            out _,
            out _);

        Assert.True(hasMay);
        Assert.Equal(new DateOnly(2026, 5, 15), mayDueOn);
        Assert.Equal(3, mayOccurrence);
        Assert.Equal(3, mayTotal);
        Assert.False(hasJune);
    }

    [Fact]
    public void CreateInstallmentSeries_ShouldKeepProgressMetadata()
    {
        var series = new BillSeries(
            Guid.NewGuid(),
            "Notebook",
            320m,
            new DateOnly(2026, 3, 8),
            BillSeries.InstallmentKind,
            BillSeries.MonthlyFrequency,
            BillSeries.OccurrenceCountEndMode,
            null,
            12);

        var bill = series.CreateOccurrenceForMonth(new DateOnly(2026, 5, 1));

        Assert.Equal(new DateOnly(2026, 5, 8), bill.DueOn);
        Assert.Equal(3, bill.OccurrenceNumber);
        Assert.Equal(12, bill.TotalOccurrences);
    }

    [Fact]
    public void CreateInstallmentSeries_ShouldRequireAtLeastTwoInstallments()
    {
        var action = () => new BillSeries(
            Guid.NewGuid(),
            "Curso",
            180m,
            new DateOnly(2026, 3, 5),
            BillSeries.InstallmentKind,
            BillSeries.MonthlyFrequency,
            BillSeries.OccurrenceCountEndMode,
            null,
            1);

        var exception = Assert.Throws<ArgumentException>(action);

        Assert.Contains("Installment count must be at least 2.", exception.Message);
    }
}
