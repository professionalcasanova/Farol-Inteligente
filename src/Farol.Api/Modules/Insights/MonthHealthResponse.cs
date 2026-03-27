namespace Farol.Api.Modules.Insights;

public sealed class MonthHealthResponse
{
    public required string Status { get; init; }

    public required int Score { get; init; }

    public required MonthHealthSummaryResponse Summary { get; init; }

    public required IReadOnlyList<MonthHealthInsightResponse> Insights { get; init; }

    public required IReadOnlyList<RecommendedActionResponse> RecommendedActions { get; init; }
}
