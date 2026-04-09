using Farol.Domain.Categories;
using Farol.Domain.Ledger;

namespace Farol.Domain.Budgets;

public sealed class BudgetTemplateCategory
{
    private BudgetTemplateCategory()
    {
    }

    public Guid Id { get; private set; }
    public Guid BudgetTemplateId { get; private set; }
    public Guid CategoryId { get; private set; }
    public decimal PlannedAmount { get; private set; }

    public BudgetTemplateCategory(Guid budgetTemplateId, Category category, decimal plannedAmount)
    {
        ArgumentNullException.ThrowIfNull(category);

        Id = Guid.NewGuid();
        BudgetTemplateId = EnsureBudgetTemplateId(budgetTemplateId);
        category.EnsureCanBeAssignedTo(TransactionType.Expense);
        CategoryId = category.Id;
        PlannedAmount = EnsurePlannedAmount(plannedAmount);
    }

    private static Guid EnsureBudgetTemplateId(Guid budgetTemplateId)
    {
        if (budgetTemplateId == Guid.Empty)
        {
            throw new ArgumentException("Budget template category must belong to a budget template.", nameof(budgetTemplateId));
        }

        return budgetTemplateId;
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
