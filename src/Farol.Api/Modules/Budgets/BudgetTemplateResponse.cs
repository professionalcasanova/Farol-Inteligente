namespace Farol.Api.Modules.Budgets;

public sealed class BudgetTemplateResponse
{
    public required decimal TotalPlanned { get; init; }
    public required IReadOnlyList<BudgetTemplateCategoryResponse> Categories { get; init; }
}
