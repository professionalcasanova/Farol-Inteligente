using Farol.Api.Common;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Dashboard;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController(FarolDbContext dbContext) : ControllerBase
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
}
