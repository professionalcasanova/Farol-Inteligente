using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Insights;
using Farol.Domain.Bills;
using Farol.Domain.Budgets;
using Farol.Domain.Categories;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class InsightsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public InsightsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetFreeMoney_FutureMonth_ShouldKeepProjectionSeparateFromRealizedBalance()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var nextMonthStart = new DateOnly(_factory.Today.Year, _factory.Today.Month, 1).AddMonths(1);
        var expenseCategoryId = await SeedExpenseCategoryAsync();

        await SeedBudgetAsync("maria@email.com", nextMonthStart.Month, nextMonthStart.Year, expenseCategoryId, 200m);
        await SeedSeriesAsync("maria@email.com", "Internet", 160m, nextMonthStart);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<FreeMoneyResponse>(
            $"/api/insights/free-money?month={nextMonthStart.Month}&year={nextMonthStart.Year}");

        Assert.NotNull(response);
        Assert.True(response.IsProjection);
        Assert.Equal(0m, response.Balance);
        Assert.Equal(200m, response.PlannedReserve);
        Assert.Equal(160m, response.UnpaidBillsReserve);
        Assert.Equal(-360m, response.FreeToSpend);
    }

    [Fact]
    public async Task GetAlerts_FutureMonthProjection_ShouldNotRaiseLowBalanceOrPendingPressureAlerts()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var nextMonthStart = new DateOnly(_factory.Today.Year, _factory.Today.Month, 1).AddMonths(1);

        await SeedSeriesAsync("maria@email.com", "Internet", 160m, nextMonthStart);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<AlertsResponse>(
            $"/api/insights/alerts?month={nextMonthStart.Month}&year={nextMonthStart.Year}");

        Assert.NotNull(response);
        Assert.Empty(response.Alerts);
    }

    private async Task<Guid> SeedExpenseCategoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var category = Category.CreateSystem("Transporte", CategoryType.Expense);

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        return category.Id;
    }

    private async Task SeedBudgetAsync(string email, int month, int year, Guid categoryId, decimal plannedAmount)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var category = dbContext.Categories.Single(item => item.Id == categoryId);
        var budget = new MonthlyBudget(userId, month, year);

        dbContext.MonthlyBudgets.Add(budget);
        dbContext.MonthlyBudgetCategories.Add(new MonthlyBudgetCategory(budget.Id, category, plannedAmount));
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedSeriesAsync(string email, string description, decimal amount, DateOnly firstDueOn)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var series = new BillSeries(
            userId,
            description,
            amount,
            firstDueOn,
            BillSeries.RecurringKind,
            BillSeries.MonthlyFrequency,
            BillSeries.OpenEndedEndMode,
            untilDate: null,
            occurrenceCount: null);

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
