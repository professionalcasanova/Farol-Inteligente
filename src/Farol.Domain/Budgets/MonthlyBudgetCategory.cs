using Farol.Domain.Categories;
using Farol.Domain.Ledger;

namespace Farol.Domain.Budgets;

public sealed class MonthlyBudgetCategory
{
    private MonthlyBudgetCategory()
    {
    }

    public Guid Id { get; private set; }
    public Guid MonthlyBudgetId { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal PlannedAmount { get; private set; }

    public MonthlyBudgetCategory(Guid monthlyBudgetId, Category category, decimal plannedAmount)
    {
        ArgumentNullException.ThrowIfNull(category);

        Id = Guid.NewGuid();
        MonthlyBudgetId = EnsureMonthlyBudgetId(monthlyBudgetId);
        category.EnsureCanBeAssignedTo(TransactionType.Expense);
        CategoryId = category.Id;
        PlannedAmount = EnsurePlannedAmount(plannedAmount);
    }

    private static Guid EnsureMonthlyBudgetId(Guid monthlyBudgetId)
    {
        if (monthlyBudgetId == Guid.Empty)
        {
            throw new ArgumentException("Monthly budget category must belong to a monthly budget.", nameof(monthlyBudgetId));
        }

        return monthlyBudgetId;
    }

    private static decimal EnsurePlannedAmount(decimal plannedAmount)
    {
        if (plannedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(plannedAmount), "Planned amount must be greater than zero.");
        }

        return decimal.Round(plannedAmount, 2, MidpointRounding.AwayFromZero);
    }
}
