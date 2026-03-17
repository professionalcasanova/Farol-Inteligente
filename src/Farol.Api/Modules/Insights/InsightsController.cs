using Farol.Api.Common;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Insights;

[ApiController]
[Authorize]
[Route("api/insights")]
public sealed class InsightsController(FarolDbContext dbContext) : ControllerBase
{
    private const string HighSeverity = "high";
    private const string MediumSeverity = "medium";
    private const string OverdueBillsAlertType = "overdue_bills";
    private const string LowBalanceAlertType = "low_balance";
    private const string BudgetOverspentAlertType = "budget_overspent";
    private const string ManyPendingBillsAlertType = "many_pending_bills";

    [HttpGet("free-money")]
    public async Task<ActionResult<FreeMoneyResponse>> GetFreeMoney(
        [FromQuery] FreeMoneyRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        DateOnly periodStart;

        try
        {
            periodStart = new DateOnly(request.Year, request.Month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { message = "Month and year are invalid." });
        }

        var snapshot = await GetMonthlyInsightSnapshotAsync(userId, periodStart, cancellationToken);

        return Ok(new FreeMoneyResponse
        {
            Month = request.Month,
            Year = request.Year,
            TotalIncome = snapshot.TotalIncome,
            TotalExpense = snapshot.TotalExpense,
            Balance = snapshot.Balance,
            TotalPlannedBudget = snapshot.TotalPlannedBudget,
            TotalBudgetSpent = snapshot.TotalBudgetSpent,
            TotalBudgetRemaining = snapshot.TotalBudgetRemaining,
            FreeToSpend = snapshot.FreeToSpend
        });
    }

    [HttpGet("alerts")]
    public async Task<ActionResult<AlertsResponse>> GetAlerts(
        [FromQuery] FreeMoneyRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        DateOnly periodStart;

        try
        {
            periodStart = new DateOnly(request.Year, request.Month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { message = "Month and year are invalid." });
        }

        var snapshot = await GetMonthlyInsightSnapshotAsync(userId, periodStart, cancellationToken);
        var alerts = new List<AlertResponse>();

        if (snapshot.TotalOverdueBills > 0)
        {
            alerts.Add(new AlertResponse
            {
                Type = OverdueBillsAlertType,
                Severity = HighSeverity,
                Message = "Voce tem contas vencidas que precisam de atencao imediata.",
                Amount = snapshot.TotalOverdueBills,
                ActionUrl = "/bills?status=overdue"
            });
        }

        if (snapshot.FreeToSpend < snapshot.TotalIncome * 0.2m)
        {
            alerts.Add(new AlertResponse
            {
                Type = LowBalanceAlertType,
                Severity = MediumSeverity,
                Message = "Seu dinheiro livre para o mes esta baixo.",
                Amount = snapshot.FreeToSpend,
                ActionUrl = "/dashboard"
            });
        }

        if (snapshot.TotalPlannedBudget > 0 && snapshot.TotalBudgetSpent > snapshot.TotalPlannedBudget)
        {
            alerts.Add(new AlertResponse
            {
                Type = BudgetOverspentAlertType,
                Severity = HighSeverity,
                Message = "Seu orcamento do mes ja foi estourado.",
                Amount = snapshot.TotalBudgetSpent - snapshot.TotalPlannedBudget,
                ActionUrl = "/budget"
            });
        }

        if (snapshot.TotalPendingBills > snapshot.TotalIncome * 0.5m)
        {
            alerts.Add(new AlertResponse
            {
                Type = ManyPendingBillsAlertType,
                Severity = MediumSeverity,
                Message = "Voce ainda tem muitas contas para pagar neste mes.",
                Amount = snapshot.TotalPendingBills,
                ActionUrl = "/bills"
            });
        }

        return Ok(new AlertsResponse
        {
            Alerts = alerts
        });
    }

    private async Task<MonthlyInsightSnapshot> GetMonthlyInsightSnapshotAsync(
        Guid userId,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        var periodEnd = periodStart.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.OccurredOn >= periodStart &&
                transaction.OccurredOn < periodEnd)
            .Select(transaction => new
            {
                transaction.Type,
                transaction.Amount,
                transaction.CategoryId
            })
            .ToListAsync(cancellationToken);

        var totalIncome = transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);

        var totalExpense = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        var balance = totalIncome - totalExpense;

        var budget = await dbContext.MonthlyBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.UserId == userId &&
                    item.Month == periodStart.Month &&
                    item.Year == periodStart.Year,
                cancellationToken);

        var totalPlannedBudget = 0m;
        var totalBudgetSpent = 0m;

        if (budget is not null)
        {
            var budgetItems = await dbContext.MonthlyBudgetCategories
                .AsNoTracking()
                .Where(item => item.MonthlyBudgetId == budget.Id)
                .Select(item => new
                {
                    item.CategoryId,
                    item.PlannedAmount
                })
                .ToListAsync(cancellationToken);

            var categoryIds = budgetItems
                .Select(item => item.CategoryId)
                .ToArray();

            var spentByCategory = categoryIds.Length == 0
                ? new Dictionary<Guid, decimal>()
                : transactions
                    .Where(transaction =>
                        transaction.Type == TransactionType.Expense &&
                        transaction.CategoryId.HasValue &&
                        categoryIds.Contains(transaction.CategoryId.Value))
                    .GroupBy(transaction => transaction.CategoryId!.Value)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(transaction => transaction.Amount));

            totalPlannedBudget = budgetItems.Sum(item => item.PlannedAmount);
            totalBudgetSpent = budgetItems.Sum(item =>
            {
                spentByCategory.TryGetValue(item.CategoryId, out var spent);
                return spent;
            });
        }

        var totalBudgetRemaining = totalPlannedBudget - totalBudgetSpent;
        var reservedBudget = totalBudgetRemaining > 0 ? totalBudgetRemaining : 0;

        var bills = await dbContext.Bills
            .AsNoTracking()
            .Where(bill =>
                bill.UserId == userId &&
                bill.DueOn >= periodStart &&
                bill.DueOn < periodEnd)
            .Select(bill => new
            {
                bill.Amount,
                bill.DueOn,
                bill.IsPaid
            })
            .ToListAsync(cancellationToken);

        var totalPendingBills = bills
            .Where(bill => !bill.IsPaid && bill.DueOn >= today)
            .Sum(bill => bill.Amount);

        var totalOverdueBills = bills
            .Where(bill => !bill.IsPaid && bill.DueOn < today)
            .Sum(bill => bill.Amount);

        return new MonthlyInsightSnapshot(
            totalIncome,
            totalExpense,
            balance,
            totalPlannedBudget,
            totalBudgetSpent,
            totalBudgetRemaining,
            balance - reservedBudget,
            totalPendingBills,
            totalOverdueBills);
    }

    private sealed record MonthlyInsightSnapshot(
        decimal TotalIncome,
        decimal TotalExpense,
        decimal Balance,
        decimal TotalPlannedBudget,
        decimal TotalBudgetSpent,
        decimal TotalBudgetRemaining,
        decimal FreeToSpend,
        decimal TotalPendingBills,
        decimal TotalOverdueBills);
}
