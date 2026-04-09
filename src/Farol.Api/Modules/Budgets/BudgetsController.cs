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
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (HasDuplicateCategories(request.Categories))
        {
            return BadRequest(new ErrorResponse("Budget categories cannot be duplicated in the same payload."));
        }

        var categories = await ResolveBudgetCategoriesAsync(userId, request.Categories, cancellationToken);

        if (categories.ErrorResult is not null)
        {
            return categories.ErrorResult;
        }

        var upsertResult = await ReplaceMonthlyBudgetAsync(
            userId,
            request.Month,
            request.Year,
            request.Categories,
            categories.ResolvedCategories,
            cancellationToken);

        if (upsertResult is not null)
        {
            return upsertResult;
        }

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
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        try
        {
            _ = new DateOnly(year, month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new ErrorResponse("Month and year are invalid."));
        }

        var response = await BuildMonthlyBudgetResponseAsync(userId, month, year, cancellationToken);

        return Ok(response);
    }

    [HttpPost("template")]
    public async Task<ActionResult<BudgetTemplateResponse>> UpsertBudgetTemplate(
        [FromBody] IReadOnlyList<CreateMonthlyBudgetCategoryRequest> categories,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        if (HasDuplicateCategories(categories))
        {
            return BadRequest(new ErrorResponse("Budget categories cannot be duplicated in the same payload."));
        }

        var resolvedCategories = await ResolveBudgetCategoriesAsync(userId, categories, cancellationToken);

        if (resolvedCategories.ErrorResult is not null)
        {
            return resolvedCategories.ErrorResult;
        }

        var upsertResult = await ReplaceBudgetTemplateAsync(
            userId,
            categories,
            resolvedCategories.ResolvedCategories,
            cancellationToken);

        if (upsertResult is not null)
        {
            return upsertResult;
        }

        var response = await BuildBudgetTemplateResponseAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpGet("template")]
    public async Task<ActionResult<BudgetTemplateResponse>> GetBudgetTemplate(CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        var response = await BuildBudgetTemplateResponseAsync(userId, cancellationToken);

        return Ok(response);
    }

    [HttpPost("template/apply")]
    public async Task<ActionResult<MonthlyBudgetResponse>> ApplyBudgetTemplate(
        ApplyBudgetTemplateRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token."));
        }

        try
        {
            _ = new DateOnly(request.Year, request.Month, 1);
        }
        catch (ArgumentOutOfRangeException)
        {
            return BadRequest(new ErrorResponse("Month and year are invalid."));
        }

        var template = await dbContext.BudgetTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        var templateItems = template is null
            ? Array.Empty<BudgetTemplateCategory>()
            : await dbContext.BudgetTemplateCategories
                .AsNoTracking()
                .Where(item => item.BudgetTemplateId == template.Id)
                .OrderBy(item => item.CategoryId)
                .ToArrayAsync(cancellationToken);

        var categoryIds = templateItems
            .Select(item => item.CategoryId)
            .ToArray();

        var categories = categoryIds.Length == 0
            ? new List<Category>()
            : await dbContext.Categories
                .Where(category => categoryIds.Contains(category.Id) && (category.IsSystem || category.UserId == userId))
                .ToListAsync(cancellationToken);

        var applyPayload = templateItems
            .Select(item => new CreateMonthlyBudgetCategoryRequest
            {
                CategoryId = item.CategoryId,
                Planned = item.PlannedAmount,
            })
            .ToArray();

        var upsertResult = await ReplaceMonthlyBudgetAsync(
            userId,
            request.Month,
            request.Year,
            applyPayload,
            categories,
            cancellationToken);

        if (upsertResult is not null)
        {
            return upsertResult;
        }

        var response = await BuildMonthlyBudgetResponseAsync(userId, request.Month, request.Year, cancellationToken);

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

    private async Task<BudgetTemplateResponse> BuildBudgetTemplateResponseAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var template = await dbContext.BudgetTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);

        if (template is null)
        {
            return new BudgetTemplateResponse
            {
                TotalPlanned = 0,
                Categories = Array.Empty<BudgetTemplateCategoryResponse>()
            };
        }

        var templateItems = await dbContext.BudgetTemplateCategories
            .AsNoTracking()
            .Where(item => item.BudgetTemplateId == template.Id)
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

        var categories = templateItems
            .Select(item => new BudgetTemplateCategoryResponse
            {
                CategoryId = item.CategoryId,
                CategoryName = item.CategoryName,
                Planned = item.PlannedAmount
            })
            .ToList();

        return new BudgetTemplateResponse
        {
            TotalPlanned = categories.Sum(item => item.Planned),
            Categories = categories
        };
    }

    private static bool HasDuplicateCategories(IReadOnlyList<CreateMonthlyBudgetCategoryRequest> categories)
    {
        return categories
            .GroupBy(item => item.CategoryId)
            .Any(group => group.Count() > 1);
    }

    private async Task<(ActionResult? ErrorResult, List<Category> ResolvedCategories)> ResolveBudgetCategoriesAsync(
        Guid userId,
        IReadOnlyList<CreateMonthlyBudgetCategoryRequest> items,
        CancellationToken cancellationToken)
    {
        var categoryIds = items
            .Select(item => item.CategoryId)
            .ToArray();

        var categories = categoryIds.Length == 0
            ? new List<Category>()
            : await dbContext.Categories
                .Where(category => categoryIds.Contains(category.Id) && (category.IsSystem || category.UserId == userId))
                .ToListAsync(cancellationToken);

        if (categories.Count != categoryIds.Length)
        {
            return (NotFound(new ErrorResponse("One or more categories were not found.")), new List<Category>());
        }

        if (categories.Any(category => category.Type != CategoryType.Expense))
        {
            return (BadRequest(new ErrorResponse("Budget categories must be expense categories.")), new List<Category>());
        }

        return (null, categories);
    }

    private async Task<ActionResult?> ReplaceMonthlyBudgetAsync(
        Guid userId,
        int month,
        int year,
        IReadOnlyList<CreateMonthlyBudgetCategoryRequest> items,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var existingBudget = await dbContext.MonthlyBudgets
            .SingleOrDefaultAsync(
                budget => budget.UserId == userId && budget.Month == month && budget.Year == year,
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
            budget = new MonthlyBudget(userId, month, year);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        dbContext.MonthlyBudgets.Add(budget);

        foreach (var item in items)
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
                return BadRequest(new ErrorResponse(exception.Message));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }

    private async Task<ActionResult?> ReplaceBudgetTemplateAsync(
        Guid userId,
        IReadOnlyList<CreateMonthlyBudgetCategoryRequest> items,
        IReadOnlyList<Category> categories,
        CancellationToken cancellationToken)
    {
        var existingTemplate = await dbContext.BudgetTemplates
            .SingleOrDefaultAsync(template => template.UserId == userId, cancellationToken);

        if (existingTemplate is not null)
        {
            var existingItems = await dbContext.BudgetTemplateCategories
                .Where(item => item.BudgetTemplateId == existingTemplate.Id)
                .ToListAsync(cancellationToken);

            dbContext.BudgetTemplateCategories.RemoveRange(existingItems);
            dbContext.BudgetTemplates.Remove(existingTemplate);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (items.Count == 0)
        {
            return null;
        }

        BudgetTemplate template;

        try
        {
            template = new BudgetTemplate(userId);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new ErrorResponse(exception.Message));
        }

        dbContext.BudgetTemplates.Add(template);

        foreach (var item in items)
        {
            var category = categories.Single(category => category.Id == item.CategoryId);

            try
            {
                dbContext.BudgetTemplateCategories.Add(new BudgetTemplateCategory(template.Id, category, item.Planned));
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
            {
                return BadRequest(new ErrorResponse(exception.Message));
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return null;
    }
}
