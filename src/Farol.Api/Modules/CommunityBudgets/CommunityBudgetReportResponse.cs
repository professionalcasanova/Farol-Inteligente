namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetReportResponse
{
    public required Guid Id { get; init; }
    public required Guid CommunityBudgetId { get; init; }
    public required string Reason { get; init; }
    public required string? Description { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
