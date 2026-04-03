namespace Farol.Api.Modules.Insights;

public sealed class FinancialAnalysisResponse
{
    public required string ContractVersion { get; init; }

    public required string Status { get; init; }

    public required int Score { get; init; }

    public string? Message { get; init; }

    public IReadOnlyList<string>? Reasons { get; init; }

    public IReadOnlyList<string>? Actions { get; init; }

    public int? Priority { get; init; }

    public required FinancialAnalysisSummaryResponse Summary { get; init; }

    public required IReadOnlyList<FinancialAnalysisInsightResponse> Insights { get; init; }

    public required IReadOnlyList<FinancialAnalysisRecommendedActionResponse> RecommendedActions { get; init; }
}

public sealed class FinancialAnalysisSummaryResponse
{
    public required string Message { get; init; }

    public required string Cause { get; init; }

    public required string Action { get; init; }
}

public sealed class FinancialAnalysisInsightResponse
{
    public required string Type { get; init; }

    public required string Severity { get; init; }

    public required int Priority { get; init; }

    public required string Message { get; init; }

    public required string Cause { get; init; }

    public required string Action { get; init; }
}

public sealed class FinancialAnalysisRecommendedActionResponse
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string Target { get; init; }
}
