namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetItemResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string CategoryName { get; init; }
    public required string Type { get; init; }
    public required string AllocationType { get; init; }
    public required decimal? Amount { get; init; }
    public required decimal? Percentage { get; init; }
    public required string? Notes { get; init; }
    public required int SortOrder { get; init; }
}
