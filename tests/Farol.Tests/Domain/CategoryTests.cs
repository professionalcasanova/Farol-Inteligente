using Farol.Domain.Categories;
using Farol.Domain.Ledger;

namespace Farol.Tests.Domain;

public sealed class CategoryTests
{
    [Fact]
    public void EnsureCanBeAssignedTo_ShouldThrowForIncompatibleTransactionType()
    {
        var category = Category.CreateSystem("Salário", CategoryType.Income);

        var action = () => category.EnsureCanBeAssignedTo(TransactionType.Expense);

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("Category type is not compatible with the transaction type.", exception.Message);
    }

    [Fact]
    public void Rename_ShouldThrowForSystemCategory()
    {
        var category = Category.CreateSystem("Moradia", CategoryType.Expense);

        var action = () => category.Rename("Nova Moradia");

        var exception = Assert.Throws<InvalidOperationException>(action);
        Assert.Equal("System categories cannot be renamed.", exception.Message);
    }
}
