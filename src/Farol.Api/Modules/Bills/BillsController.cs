using Farol.Api.Common;
using Farol.Domain.Bills;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Bills;

[ApiController]
[Authorize]
[Route("api/bills")]
public sealed class BillsController(FarolDbContext dbContext, TimeProvider timeProvider) : ControllerBase
{
    private const string PendingStatus = "pending";
    private const string PaidStatus = "paid";
    private const string OverdueStatus = "overdue";

    [HttpPost]
    public async Task<ActionResult<BillResponse>> Create(
        CreateBillRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        Bill bill;

        try
        {
            if (request.Recurrence is null)
            {
                bill = new Bill(userId, request.Description, request.Amount, request.DueOn);
                dbContext.Bills.Add(bill);
            }
            else
            {
                var series = new BillSeries(
                    userId,
                    request.Description,
                    request.Amount,
                    request.DueOn,
                    request.Recurrence.Frequency,
                    request.Recurrence.EndMode,
                    request.Recurrence.UntilDate,
                    request.Recurrence.OccurrenceCount);

                bill = series.CreateFirstOccurrence();
                dbContext.BillSeries.Add(series);
                dbContext.Bills.Add(bill);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(bill, GetToday()));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BillResponse>>> List(
        [FromQuery] ListBillsRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (request.Month.HasValue != request.Year.HasValue)
        {
            return BadRequest(new ErrorResponse("Month and year must be provided together."));
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus))
        {
            return BadRequest(new ErrorResponse("Bill status is invalid. Use pending, paid or overdue."));
        }

        var today = GetToday();
        var query = dbContext.Bills
            .AsNoTracking()
            .Where(bill => bill.UserId == userId);

        if (request.Month.HasValue && request.Year.HasValue)
        {
            DateOnly periodStart;

            try
            {
                periodStart = new DateOnly(request.Year.Value, request.Month.Value, 1);
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest(new ErrorResponse("Month and year are invalid."));
            }

            var periodEnd = periodStart.AddMonths(1);
            await ExpandRecurringSeriesAsync(userId, periodStart, periodEnd, cancellationToken);
            query = query.Where(bill => bill.DueOn >= periodStart && bill.DueOn < periodEnd);
        }

        query = normalizedStatus switch
        {
            PaidStatus => query.Where(bill => bill.IsPaid),
            PendingStatus => query.Where(bill => !bill.IsPaid && bill.DueOn >= today),
            OverdueStatus => query.Where(bill => !bill.IsPaid && bill.DueOn < today),
            _ => query
        };

        var bills = await query
            .OrderBy(bill => bill.DueOn)
            .ThenBy(bill => bill.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return Ok(bills.Select(bill => ToResponse(bill, today)).ToList());
    }

    [HttpPatch("{id:guid}/pay")]
    public async Task<ActionResult<BillResponse>> Pay(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var bill = await dbContext.Bills
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

        if (bill is null)
        {
            return NotFound(new ErrorResponse("Bill was not found."));
        }

        bill.MarkAsPaid();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(bill, GetToday()));
    }

    [HttpPatch("{id:guid}/unpay")]
    public async Task<ActionResult<BillResponse>> Unpay(Guid id, CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var bill = await dbContext.Bills
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.UserId == userId, cancellationToken);

        if (bill is null)
        {
            return NotFound(new ErrorResponse("Bill was not found."));
        }

        bill.MarkAsUnpaid();
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(bill, GetToday()));
    }

    private static BillResponse ToResponse(Bill bill, DateOnly today)
    {
        return new BillResponse
        {
            Id = bill.Id,
            Description = bill.Description,
            Amount = bill.Amount,
            DueOn = bill.DueOn,
            BillSeriesId = bill.BillSeriesId,
            OccurrenceNumber = bill.OccurrenceNumber,
            TotalOccurrences = bill.TotalOccurrences,
            IsPaid = bill.IsPaid,
            PaidAtUtc = bill.PaidAtUtc,
            CreatedAtUtc = bill.CreatedAtUtc,
            Status = ResolveStatus(bill, today)
        };
    }

    private static string ResolveStatus(Bill bill, DateOnly today)
    {
        if (bill.IsPaid)
        {
            return PaidStatus;
        }

        return bill.DueOn < today ? OverdueStatus : PendingStatus;
    }

    private static bool TryNormalizeStatus(string? rawStatus, out string? normalizedStatus)
    {
        normalizedStatus = null;

        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            return true;
        }

        normalizedStatus = rawStatus.Trim().ToLowerInvariant();

        return normalizedStatus is PendingStatus or PaidStatus or OverdueStatus;
    }

    private DateOnly GetToday()
    {
        return DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }

    private async Task ExpandRecurringSeriesAsync(
        Guid userId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
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
