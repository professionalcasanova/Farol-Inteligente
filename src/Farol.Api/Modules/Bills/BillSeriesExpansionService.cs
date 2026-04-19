using Farol.Domain.Bills;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Bills;

public sealed class BillSeriesExpansionService(FarolDbContext dbContext)
{
    public async Task ExpandForMonthAsync(
        Guid userId,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        var periodEnd = periodStart.AddMonths(1);
        var series = await dbContext.BillSeries
            .Where(item =>
                item.UserId == userId &&
                item.IsActive &&
                item.FirstDueOn < periodEnd)
            .ToListAsync(cancellationToken);

        if (series.Count == 0)
        {
            return;
        }

        var seriesIds = series.Select(item => item.Id).ToArray();
        var existingOccurrences = await dbContext.Bills
            .Where(bill =>
                bill.UserId == userId &&
                bill.BillSeriesId.HasValue &&
                seriesIds.Contains(bill.BillSeriesId.Value) &&
                bill.DueOn >= periodStart &&
                bill.DueOn < periodEnd)
            .Select(bill => bill.BillSeriesId!.Value)
            .ToListAsync(cancellationToken);

        var existingSeriesIds = existingOccurrences.ToHashSet();

        var createdSeriesIds = new List<Guid>();

        foreach (var item in series)
        {
            if (!item.TryResolveOccurrenceForMonth(
                    periodStart,
                    out var dueOn,
                    out _,
                    out _))
            {
                continue;
            }

            if (existingSeriesIds.Contains(item.Id))
            {
                continue;
            }

            dbContext.Bills.Add(item.CreateOccurrenceForMonth(periodStart));
            existingSeriesIds.Add(item.Id);
            createdSeriesIds.Add(item.Id);
        }

        if (createdSeriesIds.Count == 0)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await DetachAddedBillsAsync();

            var persistedSeriesIds = await dbContext.Bills
                .AsNoTracking()
                .Where(bill =>
                    bill.UserId == userId &&
                    bill.BillSeriesId.HasValue &&
                    seriesIds.Contains(bill.BillSeriesId.Value) &&
                    bill.DueOn >= periodStart &&
                    bill.DueOn < periodEnd)
                .Select(bill => bill.BillSeriesId!.Value)
                .ToListAsync(cancellationToken);

            var persistedSeriesIdSet = persistedSeriesIds.ToHashSet();

            if (createdSeriesIds.All(persistedSeriesIdSet.Contains))
            {
                return;
            }

            throw;
        }
    }

    private Task DetachAddedBillsAsync()
    {
        foreach (var entry in dbContext.ChangeTracker.Entries())
        {
            if (entry.Entity is not Bill || entry.State != EntityState.Added)
            {
                continue;
            }

            entry.State = EntityState.Detached;
        }

        return Task.CompletedTask;
    }
}
