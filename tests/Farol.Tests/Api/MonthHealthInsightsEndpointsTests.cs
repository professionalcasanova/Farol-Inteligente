using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Common;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Insights;
using Farol.Domain.Bills;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class MonthHealthInsightsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public MonthHealthInsightsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMonthHealth_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/insights/month-health?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonthHealth_WithOverdueBills_ShouldReturnCritical()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedBillAsync("maria@email.com", "Energia", 300m, today.AddDays(-3));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal("overdue_bills", Assert.Single(response.Insights).Type);
        Assert.Equal("Voce tem contas vencidas que precisam de atencao imediata.", response.Summary.Message);
    }

    [Fact]
    public async Task GetMonthHealth_WithNegativeFreeMoney_ShouldReturnCritical()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Mercado", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 1000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 1300m, TransactionType.Expense, seed.CategoryIds["Mercado"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal("negative_free_money", Assert.Single(response.Insights).Type);
        Assert.Equal("Voce esta no vermelho neste mes.", response.Summary.Message);
    }

    [Fact]
    public async Task GetMonthHealth_WithBudgetOverrunAboveTolerance_ShouldReturnAttention()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            today.Month,
            today.Year,
            (seed.CategoryIds["Alimentacao"], 100m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 2000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 150m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("attention", response.Status);
        var insight = Assert.Single(response.Insights);
        Assert.Equal("budget_overspent", insight.Type);
        Assert.Equal(70, insight.Priority);
    }

    [Fact]
    public async Task GetMonthHealth_WithSmallBudgetOverrun_ShouldRemainHealthy()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            today.Month,
            today.Year,
            (seed.CategoryIds["Alimentacao"], 100m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 2000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 120m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("healthy", response.Status);
        Assert.Empty(response.Insights);
        Assert.Equal("Seu mes esta sob controle ate aqui.", response.Summary.Message);
    }

    [Fact]
    public async Task GetMonthHealth_WithPendingBillsPressure_ShouldReturnAttention()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Mercado", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 1200m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 1100m, TransactionType.Expense, seed.CategoryIds["Mercado"])
            ]);

        await SeedBillAsync("maria@email.com", "Aluguel", 80m, today.AddDays(2));
        await SeedBillAsync("maria@email.com", "Internet", 40m, today.AddDays(4));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("attention", response.Status);
        var insight = Assert.Single(response.Insights);
        Assert.Equal("pending_bills_pressure", insight.Type);
        Assert.Equal(60, insight.Priority);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldOrderInsightsByPriorityAndLimitToThree()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            today.Month,
            today.Year,
            (seed.CategoryIds["Alimentacao"], 100m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 1000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 1600m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        await SeedBillAsync("maria@email.com", "Energia", 300m, today.AddDays(-2));
        await SeedBillAsync("maria@email.com", "Aluguel", 200m, today.AddDays(2));
        await SeedBillAsync("maria@email.com", "Internet", 150m, today.AddDays(5));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal(3, response.Insights.Count);
        Assert.Collection(
            response.Insights,
            first => Assert.Equal("overdue_bills", first.Type),
            second => Assert.Equal("negative_free_money", second.Type),
            third => Assert.Equal("budget_overspent", third.Type));
    }

    [Fact]
    public async Task GetMonthHealth_ShouldBeIsolatedByAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedBillAsync("maria@email.com", "Energia", 300m, today.AddDays(-2));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("healthy", response.Status);
        Assert.Empty(response.Insights);
    }

    [Fact]
    public async Task GetMonthHealth_WithInvalidMonth_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/insights/month-health?month=13&year=2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("The field Month must be between 1 and 12.", payload.Message);
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

    private async Task<Guid> SeedBillAsync(
        string email,
        string description,
        decimal amount,
        DateOnly dueOn,
        bool isPaid = false)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var bill = new Bill(userId, description, amount, dueOn);

        if (isPaid)
        {
            bill.MarkAsPaid(new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero));
        }

        dbContext.Bills.Add(bill);
        await dbContext.SaveChangesAsync();

        return bill.Id;
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Name = "Usuario Teste",
            Email = email,
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
