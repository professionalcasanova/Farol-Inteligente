namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetSummaryResponse
{
    public required Guid Id { get; init; }
    public required Guid OwnerUserId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string TargetProfile { get; init; }
    public required decimal? MonthlyIncomeReference { get; init; }
    public required bool IsPublic { get; init; }
    public required string Status { get; init; }
    public required int ReportCount { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required DateTimeOffset UpdatedAt { get; init; }
    public required int ItemCount { get; init; }
}
