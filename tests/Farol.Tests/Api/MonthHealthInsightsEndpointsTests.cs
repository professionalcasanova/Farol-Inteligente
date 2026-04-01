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

    [Fact]
    public async Task GetMonthHealth_ShouldProxyFinancialIntelligenceResponse()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            Task.FromResult(new FinancialAnalysisResponse
            {
                ContractVersion = "v1",
                Status = "critical",
                Score = 32,
                Summary = new FinancialAnalysisSummaryResponse
                {
                    Message = "Há contas vencidas no seu mês.",
                    Cause = "Você tem contas que já passaram do vencimento e isso aumenta a pressão financeira agora.",
                    Action = "Priorize quitar ou renegociar as contas vencidas hoje."
                },
                Insights =
                [
                    new FinancialAnalysisInsightResponse
                    {
                        Type = "overdue_bills",
                        Severity = "high",
                        Priority = 100,
                        Message = "Há contas vencidas no seu mês.",
                        Cause = "Você tem contas que já passaram do vencimento e isso aumenta a pressão financeira agora.",
                        Action = "Priorize quitar ou renegociar as contas vencidas hoje."
                    }
                ],
                RecommendedActions =
                [
                    new FinancialAnalysisRecommendedActionResponse
                    {
                        Id = "review_overdue_bills",
                        Label = "Resolver contas vencidas",
                        Target = "/bills?status=overdue"
                    }
                ]
            });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal(32, response.Score);
        Assert.Equal("Há contas vencidas no seu mês.", response.Summary.Message);
        Assert.Equal("overdue_bills", Assert.Single(response.Insights).Type);
        Assert.Equal("review_overdue_bills", Assert.Single(response.RecommendedActions).Id);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldSendBillsSnapshotToFinancialIntelligenceService()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var referenceDate = new DateOnly(today.Year, today.Month, Math.Min(10, DateTime.DaysInMonth(today.Year, today.Month)));

        await SeedBillAsync("maria@email.com", "Energia", 300m, referenceDate.AddDays(-3));
        await SeedBillAsync("maria@email.com", "Aluguel", 180m, referenceDate);
        await SeedBillAsync("maria@email.com", "Internet", 120m, referenceDate);
        await SeedBillAsync("maria@email.com", "Seguro", 90m, referenceDate);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"/api/insights/month-health?month={referenceDate.Month}&year={referenceDate.Year}");

        response.EnsureSuccessStatusCode();
        var request = _factory.FinancialIntelligenceClient.LastRequest;

        Assert.NotNull(request);
        Assert.Equal(1, request.Bills.OverdueCount);
        Assert.Equal(300m, request.Bills.OverdueAmount);
        Assert.Equal(3, request.Bills.PendingCount);
        Assert.Equal(390m, request.Bills.PendingAmount);
        Assert.Equal(3, request.Bills.Upcoming7DaysCount);
        Assert.Equal(390m, request.Bills.Upcoming7DaysAmount);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldUsePlannedBudgetRemainingWhenBuildingSnapshot()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salário", CategoryType.Income),
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
                (today, "Salário", 16000m, TransactionType.Income, seed.CategoryIds["Salário"]),
                (today, "Lazer", 15000m, TransactionType.Expense, seed.CategoryIds["Lazer"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"/api/insights/month-health?month={today.Month}&year={today.Year}");

        response.EnsureSuccessStatusCode();
        var request = _factory.FinancialIntelligenceClient.LastRequest;

        Assert.NotNull(request);
        Assert.Equal(1000m, request.Totals.Balance);
        Assert.Equal(500m, request.Totals.BudgetRemaining);
        Assert.Equal(500m, request.Totals.FreeToSpend);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldSendAggregatedCategoriesToFinancialIntelligenceService()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salário", CategoryType.Income),
            ("Delivery", CategoryType.Expense),
            ("Mercado", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "Salário", 5000m, TransactionType.Income, seed.CategoryIds["Salário"]),
                (today, "Almoço", 100m, TransactionType.Expense, seed.CategoryIds["Delivery"]),
                (today, "Jantar", 80m, TransactionType.Expense, seed.CategoryIds["Delivery"]),
                (today, "Compras", 250m, TransactionType.Expense, seed.CategoryIds["Mercado"])
            ]);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"/api/insights/month-health?month={today.Month}&year={today.Year}");

        response.EnsureSuccessStatusCode();
        var request = _factory.FinancialIntelligenceClient.LastRequest;

        Assert.NotNull(request);
        Assert.Contains(request.Categories, item =>
            item.Name == "Delivery" &&
            item.Type == "expense" &&
            item.Amount == 180m);
        Assert.Contains(request.Categories, item =>
            item.Name == "Salário" &&
            item.Type == "income" &&
            item.Amount == 5000m);
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

        var response = await client.GetAsync($"/api/insights/month-health?month={today.Month}&year={today.Year}");

        response.EnsureSuccessStatusCode();
        var request = _factory.FinancialIntelligenceClient.LastRequest;

        Assert.NotNull(request);
        Assert.Equal(0, request.Bills.OverdueCount);
        Assert.Equal(0m, request.Bills.OverdueAmount);
    }

    [Fact]
    public async Task GetMonthHealth_WhenFinancialIntelligenceIsUnavailable_ShouldReturnServiceUnavailable()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            throw new FinancialIntelligenceUnavailableException("Service unavailable.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("A inteligência financeira está indisponível no momento.", payload.Message);
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
