namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetRequest
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string TargetProfile { get; init; } = string.Empty;
    public decimal? MonthlyIncomeReference { get; init; }
    public bool IsPublic { get; init; } = true;
    public string? Status { get; init; }
    public IReadOnlyList<CommunityBudgetItemRequest> Items { get; init; } = [];
}
