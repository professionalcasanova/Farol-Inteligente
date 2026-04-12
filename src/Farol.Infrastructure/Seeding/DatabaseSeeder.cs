using System.Data;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Infrastructure.Seeding;

public static class DatabaseSeeder
{
    public static async Task SeedSystemCategoriesAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        if (!await CategoriesTableExistsAsync(dbContext, cancellationToken))
        {
            return;
        }

        var existingCategories = await dbContext.Categories
            .Where(category => category.IsSystem)
            .ToListAsync(cancellationToken);

        foreach (var (type, name) in CategorySeed.SystemCategories)
        {
            var matches = existingCategories
                .Where(category => category.Type == type && CategorySeed.ResolveCanonicalName(type, category.Name) == name)
                .ToList();

            if (matches.Count == 0)
            {
                var createdCategory = Category.CreateSystem(name, type);
                dbContext.Categories.Add(createdCategory);
                existingCategories.Add(createdCategory);
                continue;
            }

            var keeper = matches.FirstOrDefault(category => string.Equals(category.Name, name, StringComparison.Ordinal))
                ?? matches[0];

            if (!string.Equals(keeper.Name, name, StringComparison.Ordinal))
            {
                keeper.CorrectSystemName(name);
            }

            foreach (var duplicate in matches.Where(category => category.Id != keeper.Id))
            {
                await ReassignCategoryReferencesAsync(dbContext, duplicate.Id, keeper.Id, cancellationToken);
                dbContext.Categories.Remove(duplicate);
                existingCategories.Remove(duplicate);
            }
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<bool> CategoriesTableExistsAsync(
        FarolDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
            dbContext.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.InMemory",
            StringComparison.Ordinal))
        {
            return true;
        }

        var connection = dbContext.Database.GetDbConnection();
        var mustCloseConnection = connection.State != ConnectionState.Open;

        if (mustCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.tables
                    WHERE table_schema = 'public'
                      AND table_name = 'categories'
                );
                """;

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is bool exists && exists;
        }
        finally
        {
            if (mustCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task ReassignCategoryReferencesAsync(
        FarolDbContext dbContext,
        Guid sourceCategoryId,
        Guid targetCategoryId,
        CancellationToken cancellationToken)
    {
        var transactions = await dbContext.Transactions
            .Where(item => item.CategoryId == sourceCategoryId)
            .ToListAsync(cancellationToken);

        foreach (var transaction in transactions)
        {
            dbContext.Entry(transaction)
                .Property(nameof(Transaction.CategoryId))
                .CurrentValue = targetCategoryId;
        }

        await MergeBudgetTemplateCategoriesAsync(dbContext, sourceCategoryId, targetCategoryId, cancellationToken);
        await MergeMonthlyBudgetCategoriesAsync(dbContext, sourceCategoryId, targetCategoryId, cancellationToken);
    }

    private static async Task MergeBudgetTemplateCategoriesAsync(
        FarolDbContext dbContext,
        Guid sourceCategoryId,
        Guid targetCategoryId,
        CancellationToken cancellationToken)
    {
        var sourceItems = await dbContext.BudgetTemplateCategories
            .Where(item => item.CategoryId == sourceCategoryId)
            .ToListAsync(cancellationToken);

        foreach (var sourceItem in sourceItems)
        {
            var targetItem = await dbContext.BudgetTemplateCategories
                .SingleOrDefaultAsync(
                    item => item.BudgetTemplateId == sourceItem.BudgetTemplateId && item.CategoryId == targetCategoryId,
                    cancellationToken);

            if (targetItem is null)
            {
                dbContext.Entry(sourceItem)
                    .Property(nameof(BudgetTemplateCategory.CategoryId))
                    .CurrentValue = targetCategoryId;
                continue;
            }

            dbContext.Entry(targetItem)
                .Property(nameof(BudgetTemplateCategory.PlannedAmount))
                .CurrentValue = targetItem.PlannedAmount + sourceItem.PlannedAmount;

            dbContext.BudgetTemplateCategories.Remove(sourceItem);
        }
    }

    private static async Task MergeMonthlyBudgetCategoriesAsync(
        FarolDbContext dbContext,
        Guid sourceCategoryId,
        Guid targetCategoryId,
        CancellationToken cancellationToken)
    {
        var sourceItems = await dbContext.MonthlyBudgetCategories
            .Where(item => item.CategoryId == sourceCategoryId)
            .ToListAsync(cancellationToken);

        foreach (var sourceItem in sourceItems)
        {
            var targetItem = await dbContext.MonthlyBudgetCategories
                .SingleOrDefaultAsync(
                    item => item.MonthlyBudgetId == sourceItem.MonthlyBudgetId && item.CategoryId == targetCategoryId,
                    cancellationToken);

            if (targetItem is null)
            {
                dbContext.Entry(sourceItem)
                    .Property(nameof(MonthlyBudgetCategory.CategoryId))
                    .CurrentValue = targetCategoryId;
                continue;
            }

            dbContext.Entry(targetItem)
                .Property(nameof(MonthlyBudgetCategory.PlannedAmount))
                .CurrentValue = targetItem.PlannedAmount + sourceItem.PlannedAmount;

            dbContext.MonthlyBudgetCategories.Remove(sourceItem);
        }
    }
}
