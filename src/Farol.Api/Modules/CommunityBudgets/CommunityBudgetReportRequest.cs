namespace Farol.Api.Modules.CommunityBudgets;

public sealed class CommunityBudgetReportRequest
{
    public string Reason { get; init; } = string.Empty;
    public string? Description { get; init; }
}
