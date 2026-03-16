using Farol.Domain.Categories;
using Farol.Domain.Ledger;

namespace Farol.Tests.Domain;

public sealed class TransactionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_ShouldRejectAmountLessThanOrEqualToZero(decimal amount)
    {
        var account = new FinancialAccount(Guid.NewGuid(), "Conta", FinancialAccountType.BankAccount);

        var action = () => new Transaction(
            account,
            TransactionType.Expense,
            amount,
            "Mercado",
            new DateOnly(2026, 3, 16));

        Assert.Throws<ArgumentOutOfRangeException>(action);
    }

    [Fact]
    public void AssignCategory_ShouldRejectIncompatibleCategoryType()
    {
        var account = new FinancialAccount(Guid.NewGuid(), "Conta", FinancialAccountType.BankAccount);
        var transaction = new Transaction(
            account,
            TransactionType.Expense,
            100,
            "Mercado",
            new DateOnly(2026, 3, 16));
        var category = Category.CreateSystem("Salário", CategoryType.Income);

        var action = () => transaction.AssignCategory(category);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Category type is not compatible with the transaction type.", exception.Message);
    }
}
