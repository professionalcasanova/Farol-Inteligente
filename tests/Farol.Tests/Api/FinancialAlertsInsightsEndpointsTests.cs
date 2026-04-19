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

public sealed class FinancialAlertsInsightsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public FinancialAlertsInsightsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAlerts_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/insights/alerts?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnOverdueBillsAlert()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var referenceDate = new DateOnly(today.Year, today.Month, Math.Min(10, DateTime.DaysInMonth(today.Year, today.Month)));

        await SeedBillAsync("maria@email.com", "Energia", 300m, referenceDate.AddDays(-2));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={referenceDate.Month}&year={referenceDate.Year}");

        Assert.NotNull(response);
        Assert.Equal(2, response.Alerts.Count);

        var overdueAlert = Assert.Single(response.Alerts, alert => alert.Type == "overdue_bills");
        Assert.Equal("high", overdueAlert.Severity);
        Assert.Equal(300m, overdueAlert.Amount);
        Assert.Equal("/bills?status=overdue", overdueAlert.ActionUrl);
        Assert.Contains("contas vencidas", overdueAlert.Message, StringComparison.OrdinalIgnoreCase);

        var lowBalanceAlert = Assert.Single(response.Alerts, alert => alert.Type == "low_balance");
        Assert.Equal("medium", lowBalanceAlert.Severity);
        Assert.Equal(-300m, lowBalanceAlert.Amount);
        Assert.Equal("/transactions", lowBalanceAlert.ActionUrl);
        Assert.Contains("dinheiro livre", lowBalanceAlert.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnLowBalanceAlert()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 1000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Mercado", 850m, TransactionType.Expense, seed.CategoryIds["Alimentacao"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        var alert = Assert.Single(response.Alerts);
        Assert.Equal("low_balance", alert.Type);
        Assert.Equal("medium", alert.Severity);
        Assert.Equal(150m, alert.Amount);
        Assert.Equal("/transactions", alert.ActionUrl);
        Assert.Contains("dinheiro livre", alert.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAlerts_ShouldUsePlannedBudgetRemainingToCalculateLowBalance()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Transporte", CategoryType.Expense),
            ("Lazer", CategoryType.Expense));

        await SeedBudgetAsync(
            "maria@email.com",
            today.Month,
            today.Year,
            (seed.CategoryIds["Transporte"], 500m));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 16000m, TransactionType.Income, seed.CategoryIds["Salario"]),
                (today, "Lazer", 15000m, TransactionType.Expense, seed.CategoryIds["Lazer"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        var alert = Assert.Single(response.Alerts);
        Assert.Equal("low_balance", alert.Type);
        Assert.Equal(500m, alert.Amount);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnBudgetOverspentAlert()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
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

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        var alert = Assert.Single(response.Alerts);
        Assert.Equal("budget_overspent", alert.Type);
        Assert.Equal("high", alert.Severity);
        Assert.Equal(50m, alert.Amount);
        Assert.Equal("/budget", alert.ActionUrl);
        Assert.Contains("orcamento", alert.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnManyPendingBillsAlert()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salario", 1000m, TransactionType.Income, seed.CategoryIds["Salario"])
            ]);

        await SeedBillAsync("maria@email.com", "Aluguel", 600m, today.AddDays(2));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        var alert = Assert.Single(response.Alerts);
        Assert.Equal("many_pending_bills", alert.Type);
        Assert.Equal("medium", alert.Severity);
        Assert.Equal(600m, alert.Amount);
        Assert.Equal("/bills?status=pending", alert.ActionUrl);
        Assert.Contains("contas para pagar", alert.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnEmptyWhenNoRulesMatch()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Empty(response.Alerts);
    }

    [Fact]
    public async Task GetAlerts_ShouldBeIsolatedByAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        var today = _factory.Today;

        await SeedBillAsync("maria@email.com", "Energia", 300m, today.AddDays(-2));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Empty(response.Alerts);
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
            bill.MarkAsPaid(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));
        }

        dbContext.Bills.Add(bill);
        await dbContext.SaveChangesAsync();

        return bill.Id;
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
            Password = "123456"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
