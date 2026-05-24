using Farol.Api.Common;
using Farol.Domain.Budgets;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.CommunityBudgets;

[ApiController]
[Authorize]
[Route("api/community-budgets")]
public sealed class CommunityBudgetsController(FarolDbContext dbContext) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SuccessResponse<CommunityBudgetResponse>>> Create(
        CommunityBudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var buildResult = BuildBudget(userId, request);

        if (buildResult.Error is not null)
        {
            return BadRequest(new ErrorResponse(buildResult.Error, "validation_error"));
        }

        dbContext.CommunityBudgets.Add(buildResult.Budget!);
        dbContext.CommunityBudgetItems.AddRange(buildResult.Items);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<CommunityBudgetResponse>(ToResponse(buildResult.Budget!, buildResult.Items)));
    }

    [HttpGet]
    public async Task<ActionResult<SuccessResponse<IReadOnlyList<CommunityBudgetSummaryResponse>>>> ListPublic(
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out _))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var budgets = await dbContext.CommunityBudgets
            .AsNoTracking()
            .Where(budget => budget.Status == CommunityBudgetStatus.Published)
            .OrderByDescending(budget => budget.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var budgetIds = budgets
            .Select(budget => budget.Id)
            .ToArray();

        var itemBudgetIds = budgetIds.Length == 0
            ? []
            : await dbContext.CommunityBudgetItems
                .AsNoTracking()
                .Where(item => budgetIds.Contains(item.CommunityBudgetId))
                .Select(item => item.CommunityBudgetId)
                .ToListAsync(cancellationToken);
        var itemCounts = itemBudgetIds
            .GroupBy(id => id)
            .ToDictionary(group => group.Key, group => group.Count());
        var reportCounts = await LoadReportCountsAsync(budgetIds, cancellationToken);

        var response = budgets
            .Select(budget =>
            {
                itemCounts.TryGetValue(budget.Id, out var itemCount);
                reportCounts.TryGetValue(budget.Id, out var reportCount);

                return new CommunityBudgetSummaryResponse
                {
                    Id = budget.Id,
                    OwnerUserId = budget.OwnerUserId,
                    Title = budget.Title,
                    Description = budget.Description,
                    TargetProfile = budget.TargetProfile,
                    MonthlyIncomeReference = budget.MonthlyIncomeReference,
                    IsPublic = budget.IsPublic,
                    Status = ToWireValue(budget.Status),
                    ReportCount = reportCount,
                    CreatedAt = budget.CreatedAtUtc,
                    UpdatedAt = budget.UpdatedAtUtc,
                    ItemCount = itemCount
                };
            })
            .ToList();

        return Ok(new SuccessResponse<IReadOnlyList<CommunityBudgetSummaryResponse>>(response));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SuccessResponse<CommunityBudgetResponse>>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var budget = await dbContext.CommunityBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == id &&
                    (candidate.Status == CommunityBudgetStatus.Published || candidate.OwnerUserId == userId),
                cancellationToken);

        if (budget is null)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        var items = await LoadItemsAsync(budget.Id, cancellationToken);
        var reportCount = await CountReportsAsync(budget.Id, cancellationToken);

        return Ok(new SuccessResponse<CommunityBudgetResponse>(ToResponse(budget, items, reportCount)));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SuccessResponse<CommunityBudgetResponse>>> Update(
        Guid id,
        CommunityBudgetRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var budget = await dbContext.CommunityBudgets
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.OwnerUserId == userId, cancellationToken);

        if (budget is null)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        if (!budget.CanBeEditedByOwner())
        {
            return BadRequest(new ErrorResponse("Community budget cannot be edited in the current status.", "community_budget_not_editable"));
        }

        var statusResult = ResolveUserManagedStatus(request);

        if (statusResult.Error is not null)
        {
            return BadRequest(new ErrorResponse(statusResult.Error, "validation_error"));
        }

        var updateResult = BuildItems(budget.Id, request, statusResult.Status);

        if (updateResult.Error is not null)
        {
            return BadRequest(new ErrorResponse(updateResult.Error, "validation_error"));
        }

        try
        {
            budget.UpdateDetails(
                request.Title,
                request.Description,
                request.TargetProfile,
                request.MonthlyIncomeReference,
                statusResult.Status);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            InvalidOperationException)
        {
            return BadRequest(new ErrorResponse(exception.Message, "validation_error"));
        }

        var existingItems = await dbContext.CommunityBudgetItems
            .Where(item => item.CommunityBudgetId == budget.Id)
            .ToListAsync(cancellationToken);

        dbContext.CommunityBudgetItems.RemoveRange(existingItems);
        dbContext.CommunityBudgetItems.AddRange(updateResult.Items);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<CommunityBudgetResponse>(ToResponse(budget, updateResult.Items)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var budget = await dbContext.CommunityBudgets
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.OwnerUserId == userId, cancellationToken);

        if (budget is null)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        var items = await dbContext.CommunityBudgetItems
            .Where(item => item.CommunityBudgetId == budget.Id)
            .ToListAsync(cancellationToken);

        dbContext.CommunityBudgetItems.RemoveRange(items);
        dbContext.CommunityBudgets.Remove(budget);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/import")]
    public async Task<ActionResult<SuccessResponse<CommunityBudgetResponse>>> Import(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        var source = await dbContext.CommunityBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id && candidate.Status == CommunityBudgetStatus.Published, cancellationToken);

        if (source is null)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        var sourceItems = await LoadItemsAsync(source.Id, cancellationToken);
        var copy = new CommunityBudget(
            userId,
            source.Title,
            source.Description,
            source.TargetProfile,
            source.MonthlyIncomeReference,
            CommunityBudgetStatus.Draft);
        var copiedItems = sourceItems
            .Select(item => new CommunityBudgetItem(
                copy.Id,
                item.Name,
                item.CategoryName,
                item.Type,
                item.AllocationType,
                item.Amount,
                item.Percentage,
                item.Notes,
                item.SortOrder))
            .ToList();

        dbContext.CommunityBudgets.Add(copy);
        dbContext.CommunityBudgetItems.AddRange(copiedItems);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<CommunityBudgetResponse>(ToResponse(copy, copiedItems)));
    }

    [HttpPost("{id:guid}/reports")]
    public async Task<ActionResult<SuccessResponse<CommunityBudgetReportResponse>>> Report(
        Guid id,
        CommunityBudgetReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!AuthenticatedUser.TryGetUserId(User, out var userId))
        {
            return Unauthorized(new ErrorResponse("Invalid access token.", "unauthorized"));
        }

        if (!TryParseReportReason(request.Reason, out var reason))
        {
            return BadRequest(new ErrorResponse("Community budget report reason is invalid.", "validation_error"));
        }

        var budget = await dbContext.CommunityBudgets
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (budget is null)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        if (budget.OwnerUserId == userId)
        {
            return BadRequest(new ErrorResponse("Users cannot report their own community budget.", "cannot_report_own_budget"));
        }

        var alreadyReported = await dbContext.CommunityBudgetReports
            .AnyAsync(
                report => report.CommunityBudgetId == id && report.ReporterUserId == userId,
                cancellationToken);

        if (alreadyReported)
        {
            return Conflict(new ErrorResponse("Community budget was already reported by this user.", "duplicate_report"));
        }

        if (budget.Status != CommunityBudgetStatus.Published)
        {
            return NotFound(new ErrorResponse("Community budget was not found.", "community_budget_not_found"));
        }

        CommunityBudgetReport report;

        try
        {
            report = new CommunityBudgetReport(id, userId, reason, request.Description);
            budget.MarkReported();
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            return BadRequest(new ErrorResponse(exception.Message, "validation_error"));
        }

        dbContext.CommunityBudgetReports.Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SuccessResponse<CommunityBudgetReportResponse>(ToResponse(report)));
    }

    private async Task<List<CommunityBudgetItem>> LoadItemsAsync(
        Guid communityBudgetId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CommunityBudgetItems
            .AsNoTracking()
            .Where(item => item.CommunityBudgetId == communityBudgetId)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task<int> CountReportsAsync(
        Guid communityBudgetId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CommunityBudgetReports
            .AsNoTracking()
            .CountAsync(report => report.CommunityBudgetId == communityBudgetId, cancellationToken);
    }

    private async Task<Dictionary<Guid, int>> LoadReportCountsAsync(
        IReadOnlyCollection<Guid> communityBudgetIds,
        CancellationToken cancellationToken)
    {
        if (communityBudgetIds.Count == 0)
        {
            return [];
        }

        return await dbContext.CommunityBudgetReports
            .AsNoTracking()
            .Where(report => communityBudgetIds.Contains(report.CommunityBudgetId))
            .GroupBy(report => report.CommunityBudgetId)
            .Select(group => new
            {
                CommunityBudgetId = group.Key,
                Count = group.Count()
            })
            .ToDictionaryAsync(item => item.CommunityBudgetId, item => item.Count, cancellationToken);
    }

    private static (CommunityBudget? Budget, List<CommunityBudgetItem> Items, string? Error) BuildBudget(
        Guid userId,
        CommunityBudgetRequest request)
    {
        if (request is null)
        {
            return (null, [], "Community budget request is required.");
        }

        var statusResult = ResolveUserManagedStatus(request);

        if (statusResult.Error is not null)
        {
            return (null, [], statusResult.Error);
        }

        CommunityBudget budget;

        try
        {
            budget = new CommunityBudget(
                userId,
                request.Title,
                request.Description,
                request.TargetProfile,
                request.MonthlyIncomeReference,
                statusResult.Status);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException)
        {
            return (null, [], exception.Message);
        }

        var itemsResult = BuildItems(budget.Id, request, statusResult.Status);

        return itemsResult.Error is not null
            ? (null, [], itemsResult.Error)
            : (budget, itemsResult.Items, null);
    }

    private static (List<CommunityBudgetItem> Items, string? Error) BuildItems(
        Guid communityBudgetId,
        CommunityBudgetRequest request,
        CommunityBudgetStatus status)
    {
        var requestItems = request.Items ?? [];

        if (status == CommunityBudgetStatus.Published && requestItems.Count == 0)
        {
            return ([], "Public community budget must have at least one item.");
        }

        var result = new List<CommunityBudgetItem>();

        foreach (var item in requestItems)
        {
            if (!TryParseItemType(item.Type, out var itemType))
            {
                return ([], "Community budget item type is invalid.");
            }

            if (!TryParseAllocationType(item.AllocationType, out var allocationType))
            {
                return ([], "Community budget item allocation type is invalid.");
            }

            try
            {
                result.Add(new CommunityBudgetItem(
                    communityBudgetId,
                    item.Name,
                    item.CategoryName,
                    itemType,
                    allocationType,
                    item.Amount,
                    item.Percentage,
                    item.Notes,
                    item.SortOrder));
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                ArgumentOutOfRangeException or
                InvalidOperationException)
            {
                return ([], exception.Message);
            }
        }

        return (result, null);
    }

    private static CommunityBudgetResponse ToResponse(
        CommunityBudget budget,
        IReadOnlyList<CommunityBudgetItem> items,
        int reportCount = 0)
    {
        return new CommunityBudgetResponse
        {
            Id = budget.Id,
            OwnerUserId = budget.OwnerUserId,
            Title = budget.Title,
            Description = budget.Description,
            TargetProfile = budget.TargetProfile,
            MonthlyIncomeReference = budget.MonthlyIncomeReference,
            IsPublic = budget.IsPublic,
            Status = ToWireValue(budget.Status),
            ReportCount = reportCount,
            CreatedAt = budget.CreatedAtUtc,
            UpdatedAt = budget.UpdatedAtUtc,
            Items = items
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.Name)
                .Select(ToItemResponse)
                .ToList()
        };
    }

    private static CommunityBudgetReportResponse ToResponse(CommunityBudgetReport report)
    {
        return new CommunityBudgetReportResponse
        {
            Id = report.Id,
            CommunityBudgetId = report.CommunityBudgetId,
            Reason = ToWireValue(report.Reason),
            Description = report.Description,
            CreatedAt = report.CreatedAtUtc
        };
    }

    private static CommunityBudgetItemResponse ToItemResponse(CommunityBudgetItem item)
    {
        return new CommunityBudgetItemResponse
        {
            Id = item.Id,
            Name = item.Name,
            CategoryName = item.CategoryName,
            Type = ToWireValue(item.Type),
            AllocationType = ToWireValue(item.AllocationType),
            Amount = item.Amount,
            Percentage = item.Percentage,
            Notes = item.Notes,
            SortOrder = item.SortOrder
        };
    }

    private static bool TryParseItemType(string value, out CommunityBudgetItemType itemType)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        itemType = normalized switch
        {
            "income" => CommunityBudgetItemType.Income,
            "expense" => CommunityBudgetItemType.Expense,
            "reserve" => CommunityBudgetItemType.Reserve,
            "debt" => CommunityBudgetItemType.Debt,
            "investment" => CommunityBudgetItemType.Investment,
            _ => default
        };

        return normalized is "income" or "expense" or "reserve" or "debt" or "investment";
    }

    private static bool TryParseAllocationType(string value, out CommunityBudgetAllocationType allocationType)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        allocationType = normalized switch
        {
            "fixed_amount" => CommunityBudgetAllocationType.FixedAmount,
            "percentage" => CommunityBudgetAllocationType.Percentage,
            _ => default
        };

        return normalized is "fixed_amount" or "percentage";
    }

    private static (CommunityBudgetStatus Status, string? Error) ResolveUserManagedStatus(CommunityBudgetRequest request)
    {
        if (request is null)
        {
            return (default, "Community budget request is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!TryParseStatus(request.Status, out var parsedStatus))
            {
                return (default, "Community budget status is invalid.");
            }

            if (parsedStatus is not CommunityBudgetStatus.Draft and not CommunityBudgetStatus.Published)
            {
                return (default, "Community budget status can only be draft or published in user-managed flows.");
            }

            return (parsedStatus, null);
        }

        return (request.IsPublic ? CommunityBudgetStatus.Published : CommunityBudgetStatus.Draft, null);
    }

    private static bool TryParseStatus(string value, out CommunityBudgetStatus status)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        status = normalized switch
        {
            "draft" => CommunityBudgetStatus.Draft,
            "published" => CommunityBudgetStatus.Published,
            "hidden" => CommunityBudgetStatus.Hidden,
            "reported" => CommunityBudgetStatus.Reported,
            _ => default
        };

        return normalized is "draft" or "published" or "hidden" or "reported";
    }

    private static bool TryParseReportReason(string value, out CommunityBudgetReportReason reason)
    {
        var normalized = value?.Trim().ToLowerInvariant();

        reason = normalized switch
        {
            "sensitive_data" => CommunityBudgetReportReason.SensitiveData,
            "offensive_content" => CommunityBudgetReportReason.OffensiveContent,
            "spam" => CommunityBudgetReportReason.Spam,
            "misleading" => CommunityBudgetReportReason.Misleading,
            "other" => CommunityBudgetReportReason.Other,
            _ => default
        };

        return normalized is "sensitive_data" or "offensive_content" or "spam" or "misleading" or "other";
    }

    private static string ToWireValue(CommunityBudgetItemType type)
    {
        return type switch
        {
            CommunityBudgetItemType.Income => "income",
            CommunityBudgetItemType.Expense => "expense",
            CommunityBudgetItemType.Reserve => "reserve",
            CommunityBudgetItemType.Debt => "debt",
            CommunityBudgetItemType.Investment => "investment",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Community budget item type is invalid.")
        };
    }

    private static string ToWireValue(CommunityBudgetAllocationType allocationType)
    {
        return allocationType switch
        {
            CommunityBudgetAllocationType.FixedAmount => "fixed_amount",
            CommunityBudgetAllocationType.Percentage => "percentage",
            _ => throw new ArgumentOutOfRangeException(nameof(allocationType), allocationType, "Community budget item allocation type is invalid.")
        };
    }

    private static string ToWireValue(CommunityBudgetStatus status)
    {
        return status switch
        {
            CommunityBudgetStatus.Draft => "draft",
            CommunityBudgetStatus.Published => "published",
            CommunityBudgetStatus.Hidden => "hidden",
            CommunityBudgetStatus.Reported => "reported",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Community budget status is invalid.")
        };
    }

    private static string ToWireValue(CommunityBudgetReportReason reason)
    {
        return reason switch
        {
            CommunityBudgetReportReason.SensitiveData => "sensitive_data",
            CommunityBudgetReportReason.OffensiveContent => "offensive_content",
            CommunityBudgetReportReason.Spam => "spam",
            CommunityBudgetReportReason.Misleading => "misleading",
            CommunityBudgetReportReason.Other => "other",
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Community budget report reason is invalid.")
        };
    }
}
