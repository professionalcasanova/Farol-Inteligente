namespace Farol.Api.Modules.Budgets;

public sealed class MonthlyBudgetResponse
{
    public required int Month { get; init; }
    public required int Year { get; init; }
    public required decimal TotalPlanned { get; init; }
    public required decimal TotalSpent { get; init; }
    public required decimal TotalRemaining { get; init; }
    public required IReadOnlyList<MonthlyBudgetCategoryResponse> Categories { get; init; }
}
