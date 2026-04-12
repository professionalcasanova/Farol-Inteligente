using Farol.Domain.Categories;
using Farol.Infrastructure.Seeding;

namespace Farol.Tests.Seeding;

public sealed class CategorySeedTests
{
    [Fact]
    public void SystemCategories_ShouldCoverExpandedBrazilianTaxonomy()
    {
        var categories = CategorySeed.SystemCategories;

        Assert.Contains(categories, item => item == (CategoryType.Expense, "Mercado"));
        Assert.Contains(categories, item => item == (CategoryType.Expense, "Contas e servicos"));
        Assert.Contains(categories, item => item == (CategoryType.Expense, "Impostos e taxas"));
        Assert.Contains(categories, item => item == (CategoryType.Income, "Beneficios"));
        Assert.Contains(categories, item => item == (CategoryType.Income, "Reembolso"));
        Assert.Contains(categories, item => item == (CategoryType.Income, "Rendimento"));
    }

    [Theory]
    [InlineData("PIX")]
    [InlineData("Boleto")]
    [InlineData("Cartao de credito")]
    public void SystemCategories_ShouldNotTreatPaymentRailsAsCategories(string forbiddenName)
    {
        Assert.DoesNotContain(
            CategorySeed.SystemCategories,
            item => string.Equals(item.Name, forbiddenName, StringComparison.OrdinalIgnoreCase));
    }
}
