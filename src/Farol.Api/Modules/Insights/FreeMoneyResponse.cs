namespace Farol.Api.Modules.Insights;

public sealed class FreeMoneyResponse
{
    public required int Month { get; init; }
    public required int Year { get; init; }
    public required decimal TotalIncome { get; init; }
    public required decimal TotalExpense { get; init; }
    public required decimal Balance { get; init; }
    public required decimal TotalPlannedBudget { get; init; }
    public required decimal TotalBudgetSpent { get; init; }
    public required decimal TotalBudgetRemaining { get; init; }
    public required decimal PlannedReserve { get; init; }
    public required decimal UnpaidBillsReserve { get; init; }
    public required decimal FreeToSpend { get; init; }
}
