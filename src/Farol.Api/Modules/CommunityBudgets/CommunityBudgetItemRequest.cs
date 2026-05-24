namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetItemRequest
{
    public string Name { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string AllocationType { get; init; } = string.Empty;
    public decimal? Amount { get; init; }
    public decimal? Percentage { get; init; }
    public string? Notes { get; init; }
    public int SortOrder { get; init; }
}
