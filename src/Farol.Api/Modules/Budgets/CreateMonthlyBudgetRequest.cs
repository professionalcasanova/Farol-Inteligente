using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Budgets;

public sealed class CreateMonthlyBudgetRequest
{
    [Range(1, 12)]
    public int Month { get; init; }

    [Range(2000, 2100)]
    public int Year { get; init; }

    public IReadOnlyList<CreateMonthlyBudgetCategoryRequest> Categories { get; init; } =
        Array.Empty<CreateMonthlyBudgetCategoryRequest>();
}
