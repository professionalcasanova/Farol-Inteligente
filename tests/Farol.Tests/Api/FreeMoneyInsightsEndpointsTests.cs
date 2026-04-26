using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Insights;
using Farol.Domain.Bills;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class FreeMoneyInsightsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public FreeMoneyInsightsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetFreeMoney_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/insights/free-money?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldCalculateUsingTransactionsAndBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var mariaSeed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense),
            ("Transporte", CategoryType.Expense),
            ("Lazer", CategoryType.Expense));
        var joaoSeed = await SeedAccountAndCategoriesAsync(
            "joao@email.com",
            ("Salario", CategoryType.Income),
            ("Moradia", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (mariaSeed.CategoryIds["Alimentacao"], 300m),
            (mariaSeed.CategoryIds["Transporte"], 250m));

        await SeedTransactionsAsync(
            "maria@email.com",
            mariaSeed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 3000m, TransactionType.Income, mariaSeed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 10), "Mercado", 400m, TransactionType.Expense, mariaSeed.CategoryIds["Alimentacao"]),
                (new DateOnly(2026, 3, 11), "Uber", 200m, TransactionType.Expense, mariaSeed.CategoryIds["Transporte"]),
                (new DateOnly(2026, 3, 12), "Cinema", 150m, TransactionType.Expense, mariaSeed.CategoryIds["Lazer"]),
                (new DateOnly(2026, 2, 20), "Ignorar", 999m, TransactionType.Expense, mariaSeed.CategoryIds["Alimentacao"])
            ]);

        await SeedBudgetAsync(
            "joao@email.com",
            3,
            2026,
            (joaoSeed.CategoryIds["Moradia"], 1200m));

        await SeedTransactionsAsync(
            "joao@email.com",
            joaoSeed.AccountId,
            [
                (new DateOnly(2026, 3, 7), "Salario Joao", 5000m, TransactionType.Income, joaoSeed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 8), "Aluguel", 1200m, TransactionType.Expense, joaoSeed.CategoryIds["Moradia"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(3, response.Month);
        Assert.Equal(2026, response.Year);
        Assert.Equal(3000m, response.TotalIncome);
        Assert.Equal(750m, response.TotalExpense);
        Assert.Equal(2250m, response.Balance);
        Assert.Equal(550m, response.TotalPlannedBudget);
        Assert.Equal(600m, response.TotalBudgetSpent);
        Assert.Equal(-50m, response.TotalBudgetRemaining);
        Assert.Equal(0m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(2250m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldDiscountOnlyThePlannedBudgetRemaining()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Transporte", CategoryType.Expense),
            ("Lazer", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (seed.CategoryIds["Transporte"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 16000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 7), "Lazer", 15000m, TransactionType.Expense, seed.CategoryIds["Lazer"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(16000m, response.TotalIncome);
        Assert.Equal(15000m, response.TotalExpense);
        Assert.Equal(1000m, response.Balance);
        Assert.Equal(500m, response.TotalPlannedBudget);
        Assert.Equal(0m, response.TotalBudgetSpent);
        Assert.Equal(500m, response.TotalBudgetRemaining);
        Assert.Equal(500m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(500m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldNotDiscountBudgetTwiceWhenSpentIsAbovePlanned()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (seed.CategoryIds["Alimentacao"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 3000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 6), "Mercado", 700m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(2300m, response.Balance);
        Assert.Equal(500m, response.TotalPlannedBudget);
        Assert.Equal(700m, response.TotalBudgetSpent);
        Assert.Equal(-200m, response.TotalBudgetRemaining);
        Assert.Equal(0m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(2300m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldReturnBalanceWhenBudgetIsEmpty()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 1000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 6), "Mercado", 200m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(1000m, response.TotalIncome);
        Assert.Equal(200m, response.TotalExpense);
        Assert.Equal(800m, response.Balance);
        Assert.Equal(0m, response.TotalPlannedBudget);
        Assert.Equal(0m, response.TotalBudgetSpent);
        Assert.Equal(0m, response.TotalBudgetRemaining);
        Assert.Equal(0m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(800m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldDiscountUnpaidBillsFromFreeToSpend()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (seed.CategoryIds["Alimentacao"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 3000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 6), "Mercado", 200m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        await SeedBillsAsync(
            "maria@email.com",
            [
                (new DateOnly(2026, 3, 20), "Internet", 180m, false),
                (new DateOnly(2026, 3, 25), "Energia", 220m, false)
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(2800m, response.Balance);
        Assert.Equal(500m, response.TotalPlannedBudget);
        Assert.Equal(200m, response.TotalBudgetSpent);
        Assert.Equal(300m, response.TotalBudgetRemaining);
        Assert.Equal(300m, response.PlannedReserve);
        Assert.Equal(400m, response.UnpaidBillsReserve);
        Assert.Equal(2100m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldReturnZerosForUserWithoutTransactions()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(0m, response.TotalIncome);
        Assert.Equal(0m, response.TotalExpense);
        Assert.Equal(0m, response.Balance);
        Assert.Equal(0m, response.TotalPlannedBudget);
        Assert.Equal(0m, response.TotalBudgetSpent);
        Assert.Equal(0m, response.TotalBudgetRemaining);
        Assert.Equal(0m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(0m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldBeIsolatedByAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        var mariaSeed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (mariaSeed.CategoryIds["Alimentacao"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            mariaSeed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 2000m, TransactionType.Income, mariaSeed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 6), "Mercado", 150m, TransactionType.Expense, mariaSeed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(0m, response.TotalIncome);
        Assert.Equal(0m, response.TotalExpense);
        Assert.Equal(0m, response.TotalPlannedBudget);
        Assert.Equal(0m, response.TotalBudgetSpent);
        Assert.Equal(0m, response.PlannedReserve);
        Assert.Equal(0m, response.UnpaidBillsReserve);
        Assert.Equal(0m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldExposeUnpaidBillsReserveIncludingPendingAndOverdueBills()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            3,
            2026,
            (seed.CategoryIds["Alimentacao"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (new DateOnly(2026, 3, 5), "Salario", 3000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (new DateOnly(2026, 3, 6), "Mercado", 200m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        await SeedBillsAsync(
            "maria@email.com",
            [
                (new DateOnly(2026, 3, 8), "Aluguel", 700m, false),
                (new DateOnly(2026, 3, 20), "Internet", 180m, false),
                (new DateOnly(2026, 3, 25), "Energia", 220m, true)
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>("/api/insights/free-money?month=3&year=2026");

        Assert.NotNull(response);
        Assert.Equal(300m, response.PlannedReserve);
        Assert.Equal(880m, response.UnpaidBillsReserve);
        Assert.Equal(1620m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetFreeMoney_ShouldExposePredictableRecurringAndInstallmentReserves()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income));
        var nextMonthStart = new DateOnly(_factory.Today.Year, _factory.Today.Month, 1).AddMonths(1);

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (nextMonthStart, "Salario", 3000m, TransactionType.Income, seed.CategoryIds["Salario"])
            ]);
        await SeedSeriesAsync(
            "maria@email.com",
            "Internet",
            160m,
            nextMonthStart,
            BillSeries.RecurringKind,
            12);
        await SeedSeriesAsync(
            "maria@email.com",
            "Notebook",
            500m,
            nextMonthStart,
            BillSeries.InstallmentKind,
            10);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>(
            $"/api/insights/free-money?month={nextMonthStart.Month}&year={nextMonthStart.Year}");

        Assert.NotNull(response);
        Assert.Equal(660m, response.UnpaidBillsReserve);
        Assert.Equal(660m, response.PredictableObligationsReserve);
        Assert.Equal(2, response.PredictableObligationsCount);
        Assert.Equal(160m, response.RecurringBillsReserve);
        Assert.Equal(1, response.RecurringBillsCount);
        Assert.Equal(500m, response.InstallmentBillsReserve);
        Assert.Equal(1, response.InstallmentBillsCount);
        Assert.True(response.IsProjection);
        Assert.Equal(2340m, response.FreeToSpend);
    }

    private async Task<(Guid AccountId, Dictionary<string, Guid> CategoryIds)> SeedAccountAndCategoriesAsync(
        string email,
        params (string Name, CategoryType Type)[] categoryDefinitions)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var account = new FinancialAccount(userId, $"Conta {email}", FinancialAccountType.BankAccount);
        var categoryIds = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        dbContext.FinancialAccounts.Add(account);

        foreach (var (name, type) in categoryDefinitions)
        {
            var category = Category.CreateSystem(name, type);
            dbContext.Categories.Add(category);
            categoryIds[name] = category.Id;
        }

        await dbContext.SaveChangesAsync();

        return (account.Id, categoryIds);
    }

    private async Task SeedBudgetAsync(
        string email,
        int month,
        int year,
        params (Guid CategoryId, decimal PlannedAmount)[] items)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var budget = new MonthlyBudget(userId, month, year);

        dbContext.MonthlyBudgets.Add(budget);

        foreach (var (categoryId, plannedAmount) in items)
        {
            var category = dbContext.Categories.Single(item => item.Id == categoryId);
            dbContext.MonthlyBudgetCategories.Add(new MonthlyBudgetCategory(budget.Id, category, plannedAmount));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedTransactionsAsync(
        string email,
        Guid accountId,
        IReadOnlyList<(DateOnly OccurredOn, string Description, decimal Amount, TransactionType Type, Guid CategoryId)> items)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var account = dbContext.FinancialAccounts.Single(item => item.Id == accountId && item.UserId == userId);

        foreach (var item in items)
        {
            var category = dbContext.Categories.Single(category => category.Id == item.CategoryId);
            dbContext.Transactions.Add(new Transaction(
                account,
                item.Type,
                item.Amount,
                item.Description,
                item.OccurredOn,
                category));
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedBillsAsync(
        string email,
        IReadOnlyList<(DateOnly DueOn, string Description, decimal Amount, bool IsPaid)> items)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;

        foreach (var item in items)
        {
            var bill = new Bill(userId, item.Description, item.Amount, item.DueOn);

            if (item.IsPaid)
            {
                bill.MarkAsPaid(new DateTime(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc));
            }

            dbContext.Bills.Add(bill);
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task SeedSeriesAsync(
        string email,
        string description,
        decimal amount,
        DateOnly firstDueOn,
        string kind,
        int occurrenceCount)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var series = new BillSeries(
            userId,
            description,
            amount,
            firstDueOn,
            kind,
            BillSeries.MonthlyFrequency,
            BillSeries.OccurrenceCountEndMode,
            untilDate: null,
            occurrenceCount: occurrenceCount);

        dbContext.BillSeries.Add(series);
        dbContext.Bills.Add(series.CreateFirstOccurrence());
        await dbContext.SaveChangesAsync();
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Usuario Teste",
            Email = email,
            Password = "Password123"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
