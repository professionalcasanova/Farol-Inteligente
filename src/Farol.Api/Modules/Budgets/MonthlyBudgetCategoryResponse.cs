namespace Farol.Api.Modules.Budgets;

public sealed class MonthlyBudgetCategoryResponse
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required decimal Planned { get; init; }
    public required decimal Spent { get; init; }
    public required decimal Remaining { get; init; }
}
