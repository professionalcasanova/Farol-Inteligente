using Farol.Domain.Bills;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Bills;

public sealed class BillSeriesUpdateService(FarolDbContext dbContext)
{
    public const string SingleScope = "single";
    public const string ForwardScope = "forward";
    public const string SeriesScope = "series";

    public async Task ApplyUpdateAsync(
        Bill bill,
        BillSeries? series,
        UpdateBillRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedScope = NormalizeScope(request.Scope);

        if (!bill.BillSeriesId.HasValue || normalizedScope == SingleScope)
        {
            EnsureOccurrenceUpdateStaysInSameMonth(bill, request.DueOn);
            bill.UpdateDetails(request.Description, request.Amount, request.DueOn);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (series is null)
        {
            throw new InvalidOperationException("Bill series was not found.");
        }

        if (normalizedScope == ForwardScope)
        {
            await ApplyForwardUpdateAsync(bill, series, request, cancellationToken);
            return;
        }

        await ApplySeriesUpdateAsync(bill, series, request, cancellationToken);
    }

    public static string NormalizeScope(string? rawScope)
    {
        if (string.IsNullOrWhiteSpace(rawScope))
        {
            return SingleScope;
        }

        return rawScope.Trim().ToLowerInvariant();
    }

    public static bool IsSupportedScope(string normalizedScope)
    {
        return normalizedScope is SingleScope or ForwardScope or SeriesScope;
    }

    private async Task ApplyForwardUpdateAsync(
        Bill bill,
        BillSeries series,
        UpdateBillRequest request,
        CancellationToken cancellationToken)
    {
        if (bill.IsPaid)
        {
            throw new InvalidOperationException(
                "Paid recurring occurrences can only be edited with scope=single.");
        }

        var successor = series.CreateSuccessor(
            request.Description,
            request.Amount,
            request.DueOn,
            bill.OccurrenceNumber);

        dbContext.BillSeries.Add(successor);
        series.Deactivate();

        var successorPeriodStart = new DateOnly(request.DueOn.Year, request.DueOn.Month, 1);
        if (!successor.TryResolveOccurrenceForMonth(
                successorPeriodStart,
                out var successorDueOn,
                out var successorOccurrenceNumber,
                out var successorTotalOccurrences))
        {
            throw new InvalidOperationException("Bill series does not produce an occurrence for the requested month.");
        }

        bill.UpdateDetails(request.Description, request.Amount, successorDueOn);
        bill.ReassignSeries(successor.Id, successorOccurrenceNumber, successorTotalOccurrences);

        var futureOccurrences = await dbContext.Bills
            .Where(candidate =>
                candidate.UserId == bill.UserId &&
                candidate.BillSeriesId == series.Id &&
                candidate.Id != bill.Id &&
                !candidate.IsPaid &&
                candidate.OccurrenceNumber.HasValue &&
                bill.OccurrenceNumber.HasValue &&
                candidate.OccurrenceNumber.Value > bill.OccurrenceNumber.Value)
            .ToListAsync(cancellationToken);

        if (futureOccurrences.Count > 0)
        {
            dbContext.Bills.RemoveRange(futureOccurrences);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ApplySeriesUpdateAsync(
        Bill bill,
        BillSeries series,
        UpdateBillRequest request,
        CancellationToken cancellationToken)
    {
        var hasPaidOccurrences = await dbContext.Bills
            .AnyAsync(
                candidate =>
                    candidate.UserId == bill.UserId &&
                    candidate.BillSeriesId == series.Id &&
                    candidate.IsPaid,
                cancellationToken);

        if (hasPaidOccurrences)
        {
            throw new InvalidOperationException(
                "Recurring series with paid occurrences can only be edited with scope=single or scope=forward.");
        }

        series.UpdateRule(request.Description, request.Amount, request.DueOn);

        var occurrences = await dbContext.Bills
            .Where(candidate =>
                candidate.UserId == bill.UserId &&
                candidate.BillSeriesId == series.Id)
            .OrderBy(candidate => candidate.DueOn)
            .ToListAsync(cancellationToken);

        var occurrencesToRemove = new List<Bill>();

        foreach (var occurrence in occurrences)
        {
            var periodStart = new DateOnly(occurrence.DueOn.Year, occurrence.DueOn.Month, 1);

            if (!series.TryResolveOccurrenceForMonth(
                    periodStart,
                    out var dueOn,
                    out var occurrenceNumber,
                    out var totalOccurrences))
            {
                occurrencesToRemove.Add(occurrence);
                continue;
            }

            occurrence.UpdateDetails(request.Description, request.Amount, dueOn);
            occurrence.ReassignSeries(series.Id, occurrenceNumber, totalOccurrences);
        }

        if (occurrencesToRemove.Count > 0)
        {
            dbContext.Bills.RemoveRange(occurrencesToRemove);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureOccurrenceUpdateStaysInSameMonth(Bill bill, DateOnly updatedDueOn)
    {
        if (!bill.BillSeriesId.HasValue)
        {
            return;
        }

        if (bill.DueOn.Year == updatedDueOn.Year && bill.DueOn.Month == updatedDueOn.Month)
        {
            return;
        }

        throw new InvalidOperationException(
            "Recurring occurrences can only change due date inside the same month when scope=single.");
    }
}
