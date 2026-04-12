using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Farol.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Seeding;

public sealed class DatabaseSeederTests
{
    private const string CanonicalExpenseMeal = "Alimenta\u00e7\u00e3o";
    private const string LegacyDoubleEncodedMeal = "Alimenta\u00C3\u0192\u00C2\u00A7\u00C3\u0192\u00C2\u00A3o";
    private const string CanonicalExpenseUtilities = "Contas e servi\u00e7os";

    [Fact]
    public async Task SeedSystemCategoriesAsync_ShouldCanonicalizeLegacyCategoriesAndReassignTransactions()
    {
        await using var services = BuildServices();

        Guid legacyCategoryId;
        Guid canonicalCategoryId;
        Guid transactionId;

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

            await dbContext.Database.EnsureCreatedAsync();

            var userId = Guid.NewGuid();
            var account = new FinancialAccount(userId, "Conta principal", FinancialAccountType.BankAccount);
            var legacyCategory = Category.CreateSystem(LegacyDoubleEncodedMeal, CategoryType.Expense);
            var canonicalCategory = Category.CreateSystem(CanonicalExpenseMeal, CategoryType.Expense);
            var createdTransaction = new Transaction(
                account,
                TransactionType.Expense,
                85m,
                "Mercado",
                new DateOnly(2026, 4, 12),
                legacyCategory);

            dbContext.FinancialAccounts.Add(account);
            dbContext.Categories.AddRange(legacyCategory, canonicalCategory);
            dbContext.Transactions.Add(createdTransaction);
            await dbContext.SaveChangesAsync();

            legacyCategoryId = legacyCategory.Id;
            canonicalCategoryId = canonicalCategory.Id;
            transactionId = createdTransaction.Id;
        }

        await DatabaseSeeder.SeedSystemCategoriesAsync(services);

        await using var verificationScope = services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<FarolDbContext>();

        var expenseCategories = await verificationContext.Categories
            .Where(category => category.IsSystem && category.Type == CategoryType.Expense)
            .ToListAsync();

        var mealCategories = expenseCategories
            .Where(category => CategorySeed.ResolveCanonicalName(category.Type, category.Name) == CanonicalExpenseMeal)
            .ToList();
        var persistedTransaction = await verificationContext.Transactions
            .SingleAsync(item => item.Id == transactionId);

        Assert.Single(mealCategories);
        Assert.Equal(CanonicalExpenseMeal, mealCategories[0].Name);
        Assert.Equal(canonicalCategoryId, mealCategories[0].Id);
        Assert.Equal(canonicalCategoryId, persistedTransaction.CategoryId);
        Assert.False(await verificationContext.Categories.AnyAsync(category => category.Id == legacyCategoryId));
    }

    [Fact]
    public async Task SeedSystemCategoriesAsync_ShouldMergeBudgetRowsWhenLegacyAndCanonicalCategoriesCollide()
    {
        await using var services = BuildServices();

        Guid templateId;
        Guid monthlyBudgetId;
        Guid canonicalCategoryId;

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

            await dbContext.Database.EnsureCreatedAsync();

            var userId = Guid.NewGuid();
            var legacyCategory = Category.CreateSystem("Contas e servicos", CategoryType.Expense);
            var canonicalCategory = Category.CreateSystem(CanonicalExpenseUtilities, CategoryType.Expense);
            var template = new BudgetTemplate(userId);
            var monthlyBudget = new MonthlyBudget(userId, 4, 2026);

            dbContext.Categories.AddRange(legacyCategory, canonicalCategory);
            dbContext.BudgetTemplates.Add(template);
            dbContext.MonthlyBudgets.Add(monthlyBudget);
            await dbContext.SaveChangesAsync();

            dbContext.BudgetTemplateCategories.AddRange(
                new BudgetTemplateCategory(template.Id, legacyCategory, 120m),
                new BudgetTemplateCategory(template.Id, canonicalCategory, 80m));
            dbContext.MonthlyBudgetCategories.AddRange(
                new MonthlyBudgetCategory(monthlyBudget.Id, legacyCategory, 90m),
                new MonthlyBudgetCategory(monthlyBudget.Id, canonicalCategory, 60m));
            await dbContext.SaveChangesAsync();

            templateId = template.Id;
            monthlyBudgetId = monthlyBudget.Id;
            canonicalCategoryId = canonicalCategory.Id;
        }

        await DatabaseSeeder.SeedSystemCategoriesAsync(services);

        await using var verificationScope = services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider.GetRequiredService<FarolDbContext>();

        var templateRows = await verificationContext.BudgetTemplateCategories
            .Where(item => item.BudgetTemplateId == templateId)
            .ToListAsync();
        var monthlyRows = await verificationContext.MonthlyBudgetCategories
            .Where(item => item.MonthlyBudgetId == monthlyBudgetId)
            .ToListAsync();

        Assert.Single(templateRows);
        Assert.Equal(canonicalCategoryId, templateRows[0].CategoryId);
        Assert.Equal(200m, templateRows[0].PlannedAmount);

        Assert.Single(monthlyRows);
        Assert.Equal(canonicalCategoryId, monthlyRows[0].CategoryId);
        Assert.Equal(150m, monthlyRows[0].PlannedAmount);
    }

    private static ServiceProvider BuildServices()
    {
        var serviceCollection = new ServiceCollection();
        var databaseRoot = new InMemoryDatabaseRoot();
        var databaseName = $"Farol-Seeding-{Guid.NewGuid()}";

        serviceCollection.AddDbContext<FarolDbContext>(options =>
            options.UseInMemoryDatabase(databaseName, databaseRoot));

        return serviceCollection.BuildServiceProvider();
    }
}
