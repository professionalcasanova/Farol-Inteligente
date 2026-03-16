using Farol.Api.Common;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Budgets;

[ApiController]
[Authorize]
[Route("api/budgets")]
public sealed class BudgetsController(FarolDbContext dbContext) : ControllerBase
{
    [HttpPost("monthly")]
    public async Task<ActionResult<MonthlyBudgetResponse>> UpsertMonthlyBudget(
        CreateMonthlyBudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        if (HasDuplicateCategories(request.Categories))
        {
            return BadRequest(new { message = "Budget categories cannot be duplicated in the same payload." });
        }

        var categoryIds = request.Categories
            .Select(item => item.CategoryId)
            .ToArray();

        var categories = await dbContext.Categories
            .Where(category => categoryIds.Contains(category.Id) && (category.IsSystem || category.UserId == userId))
            .ToListAsync(cancellationToken);

        if (categories.Count != categoryIds.Length)
        {
            return BadRequest(new { message = "Budget contains invalid categories." });
        }

        if (categories.Any(category => category.Type != CategoryType.Expense))
        {
            return BadRequest(new { message = "Budget categories must be expense categories." });
        }

        var existingBudget = await dbContext.MonthlyBudgets
            .SingleOrDefaultAsync(
                budget => budget.UserId == userId && budget.Month == request.Month && budget.Year == request.Year,
                cancellationToken);

        if (existingBudget is not null)
        {
            var existingItems = await dbContext.MonthlyBudgetCategories
                .Where(item => item.MonthlyBudgetId == existingBudget.Id)
                .ToListAsync(cancellationToken);

            dbContext.MonthlyBudgetCategories.RemoveRange(existingItems);
            dbContext.MonthlyBudgets.Remove(existingBudget);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        MonthlyBudget budget;

        try
        {
            budget = new MonthlyBudget(userId, request.Month, request.Year);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            return BadRequest(new { message = exception.Message });
        }

        dbContext.MonthlyBudgets.Add(budget);

        foreach (var item in request.Categories)
        {
            var category = categories.Single(category => category.Id == item.CategoryId);

            try
            {
                dbContext.MonthlyBudgetCategories.Add(new MonthlyBudgetCategory(budget.Id, category, item.Planned));
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
            {
                return BadRequest(new { message = exception.Message });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var response = await BuildMonthlyBudgetResponseAsync(userId, request.Month, request.Year, cancellationToken);

        return Ok(response);
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<MonthlyBudgetResponse>> GetMonthlyBudget(
        [FromQuery] int month,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            _ = new DateOnly(year, month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new { message = "Month and year are invalid." });
        }

        var response = await BuildMonthlyBudgetResponseAsync(userId, month, year, cancellationToken);

        return Ok(response);
    }

    private async Task<MonthlyBudgetResponse> BuildMonthlyBudgetResponseAsync(
        Guid userId,
        int month,
        int year,
        CancellationToken cancellationToken)
    {
        var budget = await dbContext.MonthlyBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.UserId == userId && candidate.Month == month && candidate.Year == year,
                cancellationToken);

        if (budget is null)
        {
            return new MonthlyBudgetResponse
            {
                Month = month,
                Year = year,
                TotalPlanned = 0,
                TotalSpent = 0,
                TotalRemaining = 0,
                Categories = Array.Empty<MonthlyBudgetCategoryResponse>()
            };
        }

        var budgetItems = await dbContext.MonthlyBudgetCategories
            .AsNoTracking()
            .Where(item => item.MonthlyBudgetId == budget.Id)
            .Select(item => new
            {
                item.CategoryId,
                item.PlannedAmount,
                CategoryName = dbContext.Categories
                    .Where(category => category.Id == item.CategoryId)
                    .Select(category => category.Name)
                    .FirstOrDefault() ?? "Sem categoria"
            })
            .OrderBy(item => item.CategoryName)
            .ToListAsync(cancellationToken);

        var categoryIds = budgetItems
            .Select(item => item.CategoryId)
            .ToArray();

        var periodStart = new DateOnly(year, month, 1);
        var periodEnd = periodStart.AddMonths(1);

        var spentByCategory = categoryIds.Length == 0
            ? new Dictionary<Guid, decimal>()
            : await dbContext.Transactions
                .AsNoTracking()
                .Where(transaction =>
                    transaction.UserId == userId &&
                    transaction.Type == TransactionType.Expense &&
                    transaction.CategoryId.HasValue &&
                    categoryIds.Contains(transaction.CategoryId.Value) &&
                    transaction.OccurredOn >= periodStart &&
                    transaction.OccurredOn < periodEnd)
                .GroupBy(transaction => transaction.CategoryId!.Value)
                .Select(group => new
                {
                    CategoryId = group.Key,
                    Total = group.Sum(transaction => transaction.Amount)
                })
                .ToDictionaryAsync(item => item.CategoryId, item => item.Total, cancellationToken);

        var categories = budgetItems
            .Select(item =>
            {
                spentByCategory.TryGetValue(item.CategoryId, out var spent);

                return new MonthlyBudgetCategoryResponse
                {
                    CategoryId = item.CategoryId,
                    CategoryName = item.CategoryName,
                    Planned = item.PlannedAmount,
                    Spent = spent,
                    Remaining = item.PlannedAmount - spent
                };
            })
            .ToList();

        var totalPlanned = categories.Sum(item => item.Planned);
        var totalSpent = categories.Sum(item => item.Spent);

        return new MonthlyBudgetResponse
        {
            Month = month,
            Year = year,
            TotalPlanned = totalPlanned,
            TotalSpent = totalSpent,
            TotalRemaining = totalPlanned - totalSpent,
            Categories = categories
        };
    }

    private static bool HasDuplicateCategories(IReadOnlyList<CreateMonthlyBudgetCategoryRequest> categories)
    {
        return categories
            .GroupBy(item => item.CategoryId)
            .Any(group => group.Count() > 1);
    }
}
