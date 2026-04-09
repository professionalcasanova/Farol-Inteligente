namespace Farol.Api.Modules.Budgets;

public sealed class BudgetTemplateCategoryResponse
{
    public required Guid CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required decimal Planned { get; init; }
}
