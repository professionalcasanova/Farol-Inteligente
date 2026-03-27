namespace Farol.Api.Modules.Insights;

public sealed class FinancialAnalysisRequest
{
    public required string ContractVersion { get; init; }

    public required FinancialAnalysisReferenceRequest Reference { get; init; }

    public required FinancialAnalysisTotalsRequest Totals { get; init; }

    public required FinancialAnalysisBillsRequest Bills { get; init; }

    public required IReadOnlyList<FinancialAnalysisCategoryRequest> Categories { get; init; }
}

public sealed class FinancialAnalysisReferenceRequest
{
    public required string UserId { get; init; }

    public required int Month { get; init; }

    public required int Year { get; init; }

    public required string Currency { get; init; }
}

public sealed class FinancialAnalysisTotalsRequest
{
    public required decimal Income { get; init; }

    public required decimal Expense { get; init; }

    public required decimal Balance { get; init; }

    public required decimal PlannedBudget { get; init; }

    public required decimal BudgetSpent { get; init; }

    public required decimal BudgetRemaining { get; init; }

    public required decimal FreeToSpend { get; init; }
}

public sealed class FinancialAnalysisBillsRequest
{
    public required decimal PendingAmount { get; init; }

    public required decimal OverdueAmount { get; init; }

    public required int PendingCount { get; init; }

    public required int OverdueCount { get; init; }

    public required decimal Upcoming7DaysAmount { get; init; }

    public required int Upcoming7DaysCount { get; init; }
}

public sealed class FinancialAnalysisCategoryRequest
{
    public Guid? CategoryId { get; init; }

    public required string Name { get; init; }

    public required string Type { get; init; }

    public required decimal Amount { get; init; }
}
