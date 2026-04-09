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
            .Select(bill => new { bill.BillSeriesId, bill.DueOn })
            .ToListAsync(cancellationToken);

        var existingKeys = existingOccurrences
            .Select(item => $"{item.BillSeriesId:N}:{item.DueOn:yyyy-MM-dd}")
            .ToHashSet(StringComparer.Ordinal);

        var created = false;

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

            var key = $"{item.Id:N}:{dueOn:yyyy-MM-dd}";

            if (existingKeys.Contains(key))
            {
                continue;
            }

            dbContext.Bills.Add(item.CreateOccurrenceForMonth(periodStart));
            existingKeys.Add(key);
            created = true;
        }

        if (created)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
