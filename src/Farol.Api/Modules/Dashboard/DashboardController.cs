using Farol.Api.Common;
using Farol.Api.Modules.Bills;
using Farol.Domain.Bills;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Dashboard;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(
    FarolDbContext dbContext,
    TimeProvider timeProvider,
    BillSeriesExpansionService billSeriesExpansionService) : ControllerBase
{
    [HttpGet("monthly-summary")]
    public async Task<ActionResult<MonthlySummaryResponse>> GetMonthlySummary(
        [FromQuery] MonthlySummaryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        if (!MonthlyPeriodContext.TryCreate(
                request.Month,
                request.Year,
                timeProvider,
                out var period))
        {
            return BadRequest(new { message = "Month and year are invalid." });
        }

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.OccurredOn >= period!.Start &&
                transaction.OccurredOn < period.End)
            .Select(transaction => new
            {
                transaction.Type,
                transaction.Amount,
                transaction.CategoryId,
                CategoryName = dbContext.Categories
                    .Where(category => category.Id == transaction.CategoryId)
                    .Select(category => category.Name)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var totalIncome = transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);

        var totalExpense = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        var byCategory = transactions
            .GroupBy(transaction => new
            {
                transaction.CategoryId,
                CategoryName = transaction.CategoryName ?? "Sem categoria",
                transaction.Type
            })
            .Select(group => new MonthlySummaryCategoryResponse
            {
                CategoryId = group.Key.CategoryId,
                CategoryName = group.Key.CategoryName,
                Type = group.Key.Type,
                Total = group.Sum(transaction => transaction.Amount)
            })
            .OrderBy(item => item.Type)
            .ThenByDescending(item => item.Total)
            .ThenBy(item => item.CategoryName)
            .ToList();

        return Ok(new MonthlySummaryResponse
        {
            Month = request.Month,
            Year = request.Year,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            Balance = totalIncome - totalExpense,
            ByCategory = byCategory
        });
    }

    [HttpGet("bills-summary")]
    public async Task<ActionResult<BillsSummaryResponse>> GetBillsSummary(
        [FromQuery] MonthlySummaryRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        if (!MonthlyPeriodContext.TryCreate(
                request.Month,
                request.Year,
                timeProvider,
                out var period))
        {
            return BadRequest(new { message = "Month and year are invalid." });
        }

        await billSeriesExpansionService.ExpandForMonthAsync(
            userId,
            period!.Start,
            cancellationToken);

        var bills = await dbContext.Bills
            .AsNoTracking()
            .Where(bill =>
                bill.UserId == userId &&
                bill.DueOn >= period.Start &&
                bill.DueOn < period.End)
            .OrderBy(bill => bill.DueOn)
            .ThenBy(bill => bill.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        var seriesKindsById = await ResolveSeriesKindsAsync(bills, cancellationToken);

        var pendingBills = bills
            .Where(bill => period.IsPendingBill(bill.IsPaid, bill.DueOn))
            .ToList();

        var overdueBills = bills
            .Where(bill => period.IsOverdueBill(bill.IsPaid, bill.DueOn))
            .ToList();

        var paidBills = bills
            .Where(bill => bill.IsPaid)
            .ToList();
        var predictableBills = bills
            .Where(bill => !bill.IsPaid && bill.BillSeriesId.HasValue)
            .ToList();
        var recurringBills = predictableBills
            .Where(bill =>
                bill.BillSeriesId.HasValue &&
                seriesKindsById.TryGetValue(bill.BillSeriesId.Value, out var kind) &&
                kind == BillSeries.RecurringKind)
            .ToList();
        var installmentBills = predictableBills
            .Where(bill =>
                bill.BillSeriesId.HasValue &&
                seriesKindsById.TryGetValue(bill.BillSeriesId.Value, out var kind) &&
                kind == BillSeries.InstallmentKind)
            .ToList();

        var upcoming = pendingBills
            .OrderBy(bill => bill.DueOn)
            .ThenBy(bill => bill.CreatedAtUtc)
            .Take(5)
            .Select(bill => new BillsSummaryUpcomingResponse
            {
                Id = bill.Id,
                Description = bill.Description,
                Amount = bill.Amount,
                DueOn = bill.DueOn,
                Status = period.ResolveBillStatus(bill.IsPaid, bill.DueOn),
                SeriesKind = bill.BillSeriesId.HasValue &&
                    seriesKindsById.TryGetValue(bill.BillSeriesId.Value, out var kind)
                    ? kind
                    : null,
                OccurrenceNumber = bill.OccurrenceNumber,
                TotalOccurrences = bill.TotalOccurrences
            })
            .ToList();

        return Ok(new BillsSummaryResponse
        {
            TotalPending = pendingBills.Sum(bill => bill.Amount),
            TotalOverdue = overdueBills.Sum(bill => bill.Amount),
            TotalPaid = paidBills.Sum(bill => bill.Amount),
            PredictableTotal = predictableBills.Sum(bill => bill.Amount),
            RecurringTotal = recurringBills.Sum(bill => bill.Amount),
            InstallmentTotal = installmentBills.Sum(bill => bill.Amount),
            CountPending = pendingBills.Count,
            CountOverdue = overdueBills.Count,
            CountPaid = paidBills.Count,
            CountPredictable = predictableBills.Count,
            CountRecurring = recurringBills.Count,
            CountInstallment = installmentBills.Count,
            Upcoming = upcoming
        });
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
