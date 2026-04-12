using Farol.Domain.Categories;
using Farol.Infrastructure.Seeding;

namespace Farol.Tests.Seeding;

public sealed class CategorySeedTests
{
    private const string CanonicalExpenseMeal = "Alimenta\u00e7\u00e3o";
    private const string CanonicalExpenseUtilities = "Contas e servi\u00e7os";
    private const string CanonicalIncomeBenefits = "Benef\u00edcios";
    private const string CanonicalIncomeSalary = "Sal\u00e1rio";
    private const string CanonicalIncomeTransfer = "Transfer\u00eancia recebida";

    [Fact]
    public void SystemCategories_ShouldCoverExpandedBrazilianTaxonomy()
    {
        var categories = CategorySeed.SystemCategories;

        Assert.Contains(categories, item => item == (CategoryType.Expense, "Mercado"));
        Assert.Contains(categories, item => item == (CategoryType.Expense, CanonicalExpenseUtilities));
        Assert.Contains(categories, item => item == (CategoryType.Expense, "Impostos e taxas"));
        Assert.Contains(categories, item => item == (CategoryType.Income, CanonicalIncomeBenefits));
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

    [Theory]
    [InlineData(CategoryType.Expense, "Alimentacao", CanonicalExpenseMeal)]
    [InlineData(CategoryType.Expense, "Alimenta\u00C3\u00A7\u00C3\u00A3o", CanonicalExpenseMeal)]
    [InlineData(CategoryType.Expense, "Alimenta\u00C3\u0192\u00C2\u00A7\u00C3\u0192\u00C2\u00A3o", CanonicalExpenseMeal)]
    [InlineData(CategoryType.Income, "Salario", CanonicalIncomeSalary)]
    [InlineData(CategoryType.Income, "Transferencia recebida", CanonicalIncomeTransfer)]
    [InlineData(CategoryType.Income, "Transfer\u00C3\u00AAncia recebida", CanonicalIncomeTransfer)]
    public void ResolveCanonicalName_ShouldMapLegacySpellingsToCanonicalNames(
        CategoryType type,
        string legacyName,
        string expectedCanonicalName)
    {
        var canonicalName = CategorySeed.ResolveCanonicalName(type, legacyName);

        Assert.Equal(expectedCanonicalName, canonicalName);
    }
}
