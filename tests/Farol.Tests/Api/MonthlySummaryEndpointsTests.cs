using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Dashboard;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class MonthlySummaryEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public MonthlySummaryEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMonthlySummary_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/monthly-summary?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonthlySummary_ShouldReturnTotalsAndGroupByCategoryForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");

        await SeedMonthlySummaryDataAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var summary = await client.GetFromJsonAsync<MonthlySummaryResponse>("/api/dashboard/monthly-summary?month=3&year=2026");

        Assert.NotNull(summary);
        Assert.Equal(3, summary.Month);
        Assert.Equal(2026, summary.Year);
        Assert.Equal(3000m, summary.TotalIncome);
        Assert.Equal(350m, summary.TotalExpense);
        Assert.Equal(2650m, summary.Balance);
        Assert.Equal(3, summary.ByCategory.Count);
        Assert.Contains(summary.ByCategory, item => item.CategoryName == "Salario" && item.Type == TransactionType.Income && item.Total == 3000m);
        Assert.Contains(summary.ByCategory, item => item.CategoryName == "Alimentacao" && item.Type == TransactionType.Expense && item.Total == 250m);
        Assert.Contains(summary.ByCategory, item => item.CategoryName == "Transporte" && item.Type == TransactionType.Expense && item.Total == 100m);
    }

    private async Task SeedMonthlySummaryDataAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        var maria = dbContext.Users.Single(user => user.Email == "maria@email.com");
        var joao = dbContext.Users.Single(user => user.Email == "joao@email.com");

        var mariaAccount = new FinancialAccount(maria.Id, "Conta Maria", FinancialAccountType.BankAccount);
        var joaoAccount = new FinancialAccount(joao.Id, "Conta Joao", FinancialAccountType.BankAccount);
        var salaryCategory = Category.CreateSystem("Salario", CategoryType.Income);
        var foodCategory = Category.CreateSystem("Alimentacao", CategoryType.Expense);
        var transportCategory = Category.CreateSystem("Transporte", CategoryType.Expense);

        dbContext.FinancialAccounts.AddRange(mariaAccount, joaoAccount);
        dbContext.Categories.AddRange(salaryCategory, foodCategory, transportCategory);
        await dbContext.SaveChangesAsync();

        var mariaSalary = new Transaction(
            mariaAccount,
            TransactionType.Income,
            3000m,
            "Salario",
            new DateOnly(2026, 3, 5),
            salaryCategory);

        var mariaFood = new Transaction(
            mariaAccount,
            TransactionType.Expense,
            250m,
            "Mercado",
            new DateOnly(2026, 3, 10),
            foodCategory);

        var mariaTransport = new Transaction(
            mariaAccount,
            TransactionType.Expense,
            100m,
            "Uber",
            new DateOnly(2026, 3, 11),
            transportCategory);

        var mariaOtherMonth = new Transaction(
            mariaAccount,
            TransactionType.Expense,
            999m,
            "Ignorar",
            new DateOnly(2026, 2, 20),
            foodCategory);

        var joaoSalary = new Transaction(
            joaoAccount,
            TransactionType.Income,
            4500m,
            "Salario Joao",
            new DateOnly(2026, 3, 7),
            salaryCategory);

        dbContext.Transactions.AddRange(mariaSalary, mariaFood, mariaTransport, mariaOtherMonth, joaoSalary);
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

        var authResponse = await ApiTestResponseReader.ReadDataAsync<AuthResponse>(response);

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
