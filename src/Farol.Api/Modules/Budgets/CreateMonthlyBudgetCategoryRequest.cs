using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Budgets;

public sealed class CreateMonthlyBudgetCategoryRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Range(
        typeof(decimal),
        "0.01",
        "999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal Planned { get; init; }
}
