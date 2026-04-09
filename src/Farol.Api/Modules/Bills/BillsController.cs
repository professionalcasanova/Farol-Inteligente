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
public sealed class BillsController(
    FarolDbContext dbContext,
    TimeProvider timeProvider,
    BillSeriesExpansionService billSeriesExpansionService) : ControllerBase
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
                    request.Recurrence.Kind,
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
            return BadRequest(new ErrorResponse(NormalizeDomainErrorMessage(exception.Message)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(
            bill,
            GetToday(),
            request.Recurrence?.Kind));
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
            await billSeriesExpansionService.ExpandForMonthAsync(
                userId,
                periodStart,
                cancellationToken);
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

        var seriesKindById = await ResolveSeriesKindsAsync(bills, cancellationToken);

        return Ok(bills.Select(bill => ToResponse(
            bill,
            today,
            bill.BillSeriesId.HasValue && seriesKindById.TryGetValue(bill.BillSeriesId.Value, out var kind)
                ? kind
                : null)).ToList());
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

        return Ok(ToResponse(
            bill,
            GetToday(),
            await ResolveSeriesKindAsync(bill.BillSeriesId, cancellationToken)));
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

        return Ok(ToResponse(
            bill,
            GetToday(),
            await ResolveSeriesKindAsync(bill.BillSeriesId, cancellationToken)));
    }

    private static BillResponse ToResponse(Bill bill, DateOnly today, string? seriesKind)
    {
        return new BillResponse
        {
            Id = bill.Id,
            Description = bill.Description,
            Amount = bill.Amount,
            DueOn = bill.DueOn,
            BillSeriesId = bill.BillSeriesId,
            SeriesKind = seriesKind,
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

    private static string NormalizeDomainErrorMessage(string message)
    {
        var parameterSuffixIndex = message.IndexOf(" (Parameter '", StringComparison.Ordinal);

        if (parameterSuffixIndex < 0)
        {
            return message;
        }

        return message[..parameterSuffixIndex];
    }

    private async Task<string?> ResolveSeriesKindAsync(Guid? billSeriesId, CancellationToken cancellationToken)
    {
        if (!billSeriesId.HasValue)
        {
            return null;
        }

        return await dbContext.BillSeries
            .Where(series => series.Id == billSeriesId.Value)
            .Select(series => series.Kind)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> ResolveSeriesKindsAsync(
        IReadOnlyCollection<Bill> bills,
        CancellationToken cancellationToken)
    {
        var seriesIds = bills
            .Where(bill => bill.BillSeriesId.HasValue)
            .Select(bill => bill.BillSeriesId!.Value)
            .Distinct()
            .ToArray();

        if (seriesIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.BillSeries
            .AsNoTracking()
            .Where(series => seriesIds.Contains(series.Id))
            .ToDictionaryAsync(series => series.Id, series => series.Kind, cancellationToken);
    }
}
