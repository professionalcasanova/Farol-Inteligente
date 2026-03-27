using Farol.Domain.Bills;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Domain.Users;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Infrastructure.Seeding;

public static class DemoScenarioSeeder
{
    public const string DemoPassword = "123456";

    public static IReadOnlyList<DemoScenarioSeed> Scenarios { get; } =
    [
        new("healthy_surplus", "Cenário 1", "Mês saudável com sobra", "saudavel@farol.local", "Ana Saudável", "healthy"),
        new("tight_without_delay", "Cenário 2", "Mês apertado sem atraso", "apertado@farol.local", "Bruno Apertado", "attention"),
        new("negative_balance", "Cenário 3", "Saldo negativo", "negativo@farol.local", "Carla Negativo", "critical"),
        new("negative_with_overdue", "Cenário 4", "Saldo negativo e contas vencidas", "vencido@farol.local", "Diego Vencido", "critical"),
        new("budget_overspent", "Cenário 5", "Orçamento estourado", "orcamento@farol.local", "Erika Orçamento", "attention"),
        new("high_non_essential", "Cenário 6", "Gasto não essencial alto", "lazer@farol.local", "Fábio Lazer", "attention"),
        new("short_term_pressure", "Cenário 7", "Curto prazo pressionado", "curtoprazo@farol.local", "Gabi Curto Prazo", "critical"),
        new("low_data", "Cenário 8", "Poucos dados", "poucosdados@farol.local", "Hugo Começando", "healthy"),
        new("multiple_problems", "Cenário 9", "Múltiplos problemas simultâneos", "multiplos@farol.local", "Iara Múltiplos", "critical")
    ];

    public static async Task SeedDemoScenariosAsync(
        IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<PasswordService>();

        var categoryLookup = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem)
            .ToListAsync(cancellationToken);

        if (categoryLookup.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var month = today.Month;
        var year = today.Year;

        foreach (var scenario in Scenarios)
        {
            var exists = await dbContext.Users
                .AsNoTracking()
                .AnyAsync(user => user.Email == scenario.Email, cancellationToken);

            if (exists)
            {
                continue;
            }

            var user = CreateUser(passwordService, scenario.Name, scenario.Email);
            var account = new FinancialAccount(user.Id, "Conta principal", FinancialAccountType.BankAccount);

            dbContext.Users.Add(user);
            dbContext.FinancialAccounts.Add(account);

            await SeedScenarioDataAsync(
                dbContext,
                account,
                categoryLookup,
                scenario.Key,
                today,
                month,
                year,
                cancellationToken);
        }

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedScenarioDataAsync(
        FarolDbContext dbContext,
        FinancialAccount account,
        IReadOnlyList<Category> systemCategories,
        string scenarioKey,
        DateOnly today,
        int month,
        int year,
        CancellationToken cancellationToken)
    {
        var income = GetSystemCategory(systemCategories, CategoryType.Income, "Freelance");
        var housing = GetSystemCategory(systemCategories, CategoryType.Expense, "Moradia");
        var transport = GetSystemCategory(systemCategories, CategoryType.Expense, "Transporte");
        var leisure = GetSystemCategory(systemCategories, CategoryType.Expense, "Lazer");
        var subscriptions = GetSystemCategory(systemCategories, CategoryType.Expense, "Assinaturas");
        var otherExpense = GetSystemCategory(systemCategories, CategoryType.Expense, "Outros");

        switch (scenarioKey)
        {
            case "healthy_surplus":
                AddTransaction(dbContext, account, income, 6000m, TransactionType.Income, today.AddDays(-10), "Freelance do mês");
                AddTransaction(dbContext, account, housing, 2200m, TransactionType.Expense, today.AddDays(-8), "Aluguel");
                AddTransaction(dbContext, account, transport, 400m, TransactionType.Expense, today.AddDays(-6), "Transporte");
                AddTransaction(dbContext, account, otherExpense, 300m, TransactionType.Expense, today.AddDays(-4), "Mercado");
                AddBudget(dbContext, account.UserId, month, year, (transport, 600m), (leisure, 500m));
                break;

            case "tight_without_delay":
                AddTransaction(dbContext, account, income, 3500m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 2000m, TransactionType.Expense, today.AddDays(-8), "Aluguel");
                AddTransaction(dbContext, account, transport, 300m, TransactionType.Expense, today.AddDays(-6), "Transporte");
                AddTransaction(dbContext, account, otherExpense, 400m, TransactionType.Expense, today.AddDays(-3), "Mercado");
                AddBudget(dbContext, account.UserId, month, year, (leisure, 400m));
                AddBill(dbContext, account.UserId, "Cartão", 250m, today);
                AddBill(dbContext, account.UserId, "Internet", 200m, today);
                break;

            case "negative_balance":
                AddTransaction(dbContext, account, income, 3000m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 2200m, TransactionType.Expense, today.AddDays(-8), "Aluguel");
                AddTransaction(dbContext, account, otherExpense, 1100m, TransactionType.Expense, today.AddDays(-3), "Compras do mês");
                break;

            case "negative_with_overdue":
                AddTransaction(dbContext, account, income, 3000m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 2300m, TransactionType.Expense, today.AddDays(-8), "Aluguel");
                AddTransaction(dbContext, account, otherExpense, 900m, TransactionType.Expense, today.AddDays(-4), "Mercado");
                AddBill(dbContext, account.UserId, "Energia", 250m, today.AddDays(-2));
                break;

            case "budget_overspent":
                AddTransaction(dbContext, account, income, 5000m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 1100m, TransactionType.Expense, today.AddDays(-8), "Moradia");
                AddTransaction(dbContext, account, leisure, 350m, TransactionType.Expense, today.AddDays(-6), "Lazer");
                AddTransaction(dbContext, account, otherExpense, 1050m, TransactionType.Expense, today.AddDays(-3), "Outras saídas");
                AddBudget(dbContext, account.UserId, month, year, (housing, 1000m), (leisure, 300m));
                break;

            case "high_non_essential":
                AddTransaction(dbContext, account, income, 5000m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 1200m, TransactionType.Expense, today.AddDays(-8), "Moradia");
                AddTransaction(dbContext, account, otherExpense, 400m, TransactionType.Expense, today.AddDays(-6), "Mercado");
                AddTransaction(dbContext, account, leisure, 900m, TransactionType.Expense, today.AddDays(-3), "Lazer");
                break;

            case "short_term_pressure":
                AddTransaction(dbContext, account, income, 4500m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 2500m, TransactionType.Expense, today.AddDays(-8), "Moradia");
                AddTransaction(dbContext, account, transport, 500m, TransactionType.Expense, today.AddDays(-6), "Transporte");
                AddTransaction(dbContext, account, otherExpense, 600m, TransactionType.Expense, today.AddDays(-4), "Mercado");
                AddBudget(dbContext, account.UserId, month, year, (transport, 400m));
                AddBill(dbContext, account.UserId, "Cartão", 350m, today);
                AddBill(dbContext, account.UserId, "Parcela", 400m, today);
                break;

            case "low_data":
                break;

            case "multiple_problems":
                AddTransaction(dbContext, account, income, 3000m, TransactionType.Income, today.AddDays(-10), "Receita principal");
                AddTransaction(dbContext, account, housing, 2200m, TransactionType.Expense, today.AddDays(-8), "Moradia");
                AddTransaction(dbContext, account, leisure, 1200m, TransactionType.Expense, today.AddDays(-5), "Lazer");
                AddBudget(dbContext, account.UserId, month, year, (leisure, 200m));
                AddBill(dbContext, account.UserId, "Energia", 300m, today.AddDays(-2));
                AddBill(dbContext, account.UserId, "Cartão", 250m, today);
                AddBill(dbContext, account.UserId, "Internet", 200m, today);
                break;

            default:
                throw new InvalidOperationException($"Unknown demo scenario '{scenarioKey}'.");
        }

        await Task.CompletedTask;
    }

    private static User CreateUser(
        PasswordService passwordService,
        string name,
        string email)
    {
        var user = new User(name, email, "temporary-hash");
        user.ChangePasswordHash(passwordService.HashPassword(user, DemoPassword));
        return user;
    }

    private static Category GetSystemCategory(
        IReadOnlyList<Category> categories,
        CategoryType type,
        string name)
    {
        var category = categories.SingleOrDefault(item =>
            item.Type == type &&
            string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));

        if (category is null)
        {
            throw new InvalidOperationException(
                $"System category '{name}' ({type}) was not found. Run category seeding first.");
        }

        return category;
    }

    private static void AddTransaction(
        FarolDbContext dbContext,
        FinancialAccount account,
        Category category,
        decimal amount,
        TransactionType type,
        DateOnly occurredOn,
        string description)
    {
        dbContext.Transactions.Add(new Transaction(
            account,
            type,
            amount,
            description,
            occurredOn,
            category));
    }

    private static void AddBudget(
        FarolDbContext dbContext,
        Guid userId,
        int month,
        int year,
        params (Category Category, decimal PlannedAmount)[] items)
    {
        var budget = new MonthlyBudget(userId, month, year);
        dbContext.MonthlyBudgets.Add(budget);

        foreach (var (category, plannedAmount) in items)
        {
            dbContext.MonthlyBudgetCategories.Add(new MonthlyBudgetCategory(
                budget.Id,
                category,
                plannedAmount));
        }
    }

    private static void AddBill(
        FarolDbContext dbContext,
        Guid userId,
        string description,
        decimal amount,
        DateOnly dueOn)
    {
        dbContext.Bills.Add(new Bill(userId, description, amount, dueOn));
    }
}

public sealed record DemoScenarioSeed(
    string Key,
    string Code,
    string Title,
    string Email,
    string Name,
    string ExpectedStatus);
