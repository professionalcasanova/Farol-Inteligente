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

        var periodEnd = periodStart.AddMonths(1);

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
                item => item.UserId == userId && item.Month == request.Month && item.Year == request.Year,
                cancellationToken);

        if (budget is null)
        {
            return Ok(new FreeMoneyResponse
            {
                Month = request.Month,
                Year = request.Year,
                TotalIncome = totalIncome,
                TotalExpense = totalExpense,
                Balance = balance,
                TotalPlannedBudget = 0,
                TotalBudgetSpent = 0,
                TotalBudgetRemaining = 0,
                FreeToSpend = balance
            });
        }

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

        var totalPlannedBudget = budgetItems.Sum(item => item.PlannedAmount);
        var totalBudgetSpent = budgetItems.Sum(item =>
        {
            spentByCategory.TryGetValue(item.CategoryId, out var spent);
            return spent;
        });
        var totalBudgetRemaining = totalPlannedBudget - totalBudgetSpent;
        var reservedBudget = totalBudgetRemaining > 0 ? totalBudgetRemaining : 0;

        return Ok(new FreeMoneyResponse
        {
            Month = request.Month,
            Year = request.Year,
            TotalIncome = totalIncome,
            TotalExpense = totalExpense,
            Balance = balance,
            TotalPlannedBudget = totalPlannedBudget,
            TotalBudgetSpent = totalBudgetSpent,
            TotalBudgetRemaining = totalBudgetRemaining,
            FreeToSpend = balance - reservedBudget
        });
    }
}
