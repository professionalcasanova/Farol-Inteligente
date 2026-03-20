using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Budgets;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class BudgetsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public BudgetsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMonthlyBudget_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMonthlyBudget_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/budgets/monthly?month=3&year=2026");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostMonthlyBudget_ShouldCreateBudgetForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense), ("Transporte", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 500m },
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Transporte"], Planned = 300m }
            ]
        });

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Single(dbContext.MonthlyBudgets);
        Assert.Equal(2, dbContext.MonthlyBudgetCategories.Count());
    }

    [Fact]
    public async Task PostMonthlyBudget_ShouldReplaceBudgetForSameMonthAndYear()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense), ("Transporte", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 500m },
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Transporte"], Planned = 300m }
            ]
        });

        var replaceResponse = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 700m }
            ]
        });

        replaceResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var budget = dbContext.MonthlyBudgets.Single();
        var items = dbContext.MonthlyBudgetCategories.Where(item => item.MonthlyBudgetId == budget.Id).ToList();

        Assert.Single(items);
        Assert.Equal(700m, items[0].PlannedAmount);
    }

    [Fact]
    public async Task PostMonthlyBudget_ShouldRejectDuplicateCategoryInPayload()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 500m },
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 200m }
            ]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMonthlyBudget_ShouldRejectIncomeCategory()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Salario", CategoryType.Income));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Salario"], Planned = 500m }
            ]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostMonthlyBudget_InvalidMonthOrYear_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 13,
            Year = 2026,
            Categories = []
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Contains("Month", error.Message);
    }

    [Fact]
    public async Task PostMonthlyBudget_EmptyCategories_ReturnsSuccessWithEmptyBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories = []
        });

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<MonthlyBudgetResponse>();

        Assert.NotNull(summary);
        Assert.Equal(0m, summary.TotalPlanned);
        Assert.Equal(0m, summary.TotalSpent);
        Assert.Equal(0m, summary.TotalRemaining);
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public async Task PostMonthlyBudget_EmptyCategories_ShouldClearExistingBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 500m }
            ]
        });

        createResponse.EnsureSuccessStatusCode();

        var clearResponse = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories = []
        });

        clearResponse.EnsureSuccessStatusCode();

        var summary = await clearResponse.Content.ReadFromJsonAsync<MonthlyBudgetResponse>();

        Assert.NotNull(summary);
        Assert.Equal(0m, summary.TotalPlanned);
        Assert.Equal(0m, summary.TotalSpent);
        Assert.Equal(0m, summary.TotalRemaining);
        Assert.Empty(summary.Categories);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Single(dbContext.MonthlyBudgets);
        Assert.Empty(dbContext.MonthlyBudgetCategories);
    }

    [Fact]
    public async Task PostMonthlyBudget_CategoryFromAnotherUser_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoCategories = await SeedUserOwnedCategoriesAsync("joao@email.com", ("Moradia", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = joaoCategories["Moradia"], Planned = 500m }
            ]
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("One or more categories were not found.", error.Message);
    }

    [Fact]
    public async Task PostMonthlyBudget_NonexistentCategory_ReturnsNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = Guid.NewGuid(), Planned = 500m }
            ]
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("One or more categories were not found.", error.Message);
    }

    [Fact]
    public async Task PostMonthlyBudget_PlannedAmountLessThanOrEqualZero_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 0m }
            ]
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Contains("Planned", error.Message);
    }

    [Fact]
    public async Task GetMonthlyBudget_ShouldReturnPlannedSpentAndRemaining()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var categories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense), ("Transporte", CategoryType.Expense));
        var joaoCategories = await SeedCategoriesAsync("joao@email.com", ("Moradia", CategoryType.Expense));
        await SeedAccountsAndTransactionsAsync(categories, joaoCategories);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Alimentacao"], Planned = 500m },
                new CreateMonthlyBudgetCategoryRequest { CategoryId = categories["Transporte"], Planned = 300m }
            ]
        });

        var summary = await client.GetFromJsonAsync<MonthlyBudgetResponse>("/api/budgets/monthly?month=3&year=2026");

        Assert.NotNull(summary);
        Assert.Equal(800m, summary.TotalPlanned);
        Assert.Equal(170m, summary.TotalSpent);
        Assert.Equal(630m, summary.TotalRemaining);
        Assert.Equal(2, summary.Categories.Count);
        Assert.Contains(summary.Categories, item => item.CategoryName == "Alimentacao" && item.Planned == 500m && item.Spent == 120m && item.Remaining == 380m);
        Assert.Contains(summary.Categories, item => item.CategoryName == "Transporte" && item.Planned == 300m && item.Spent == 50m && item.Remaining == 250m);
    }

    [Fact]
    public async Task GetMonthlyBudget_ShouldBeIsolatedByAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        var mariaCategories = await SeedCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        await client.PostAsJsonAsync("/api/budgets/monthly", new CreateMonthlyBudgetRequest
        {
            Month = 3,
            Year = 2026,
            Categories =
            [
                new CreateMonthlyBudgetCategoryRequest { CategoryId = mariaCategories["Alimentacao"], Planned = 500m }
            ]
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var summary = await client.GetFromJsonAsync<MonthlyBudgetResponse>("/api/budgets/monthly?month=3&year=2026");

        Assert.NotNull(summary);
        Assert.Equal(0m, summary.TotalPlanned);
        Assert.Empty(summary.Categories);
    }

    [Fact]
    public async Task GetMonthlyBudget_InvalidMonthOrYear_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("/api/budgets/monthly?month=13&year=2026");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Month and year are invalid.", error.Message);
    }

    private async Task<Dictionary<string, Guid>> SeedCategoriesAsync(string email, params (string Name, CategoryType Type)[] categoryDefinitions)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        _ = dbContext.Users.Single(user => user.Email == email).Id;
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, type) in categoryDefinitions)
        {
            var category = Category.CreateSystem(name, type);
            dbContext.Categories.Add(category);
            result[name] = category.Id;
        }

        await dbContext.SaveChangesAsync();

        return result;
    }

    private async Task<Dictionary<string, Guid>> SeedUserOwnedCategoriesAsync(string email, params (string Name, CategoryType Type)[] categoryDefinitions)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, type) in categoryDefinitions)
        {
            var category = Category.CreateUserOwned(userId, name, type);
            dbContext.Categories.Add(category);
            result[name] = category.Id;
        }

        await dbContext.SaveChangesAsync();

        return result;
    }

    private async Task SeedAccountsAndTransactionsAsync(
        IReadOnlyDictionary<string, Guid> mariaCategories,
        IReadOnlyDictionary<string, Guid> joaoCategories)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var maria = dbContext.Users.Single(user => user.Email == "maria@email.com");
        var joao = dbContext.Users.Single(user => user.Email == "joao@email.com");

        var mariaAccount = new FinancialAccount(maria.Id, "Conta Maria", FinancialAccountType.BankAccount);
        var joaoAccount = new FinancialAccount(joao.Id, "Conta Joao", FinancialAccountType.BankAccount);
        dbContext.FinancialAccounts.AddRange(mariaAccount, joaoAccount);
        await dbContext.SaveChangesAsync();

        var mariaFoodCategory = dbContext.Categories.Single(category => category.Id == mariaCategories["Alimentacao"]);
        var mariaTransportCategory = dbContext.Categories.Single(category => category.Id == mariaCategories["Transporte"]);
        var joaoHousingCategory = dbContext.Categories.Single(category => category.Id == joaoCategories["Moradia"]);

        dbContext.Transactions.AddRange(
            new Transaction(mariaAccount, TransactionType.Expense, 120m, "Mercado", new DateOnly(2026, 3, 5), mariaFoodCategory),
            new Transaction(mariaAccount, TransactionType.Expense, 50m, "Onibus", new DateOnly(2026, 3, 6), mariaTransportCategory),
            new Transaction(mariaAccount, TransactionType.Expense, 999m, "Fevereiro", new DateOnly(2026, 2, 6), mariaFoodCategory),
            new Transaction(mariaAccount, TransactionType.Income, 3000m, "Salario", new DateOnly(2026, 3, 7)),
            new Transaction(joaoAccount, TransactionType.Expense, 777m, "Aluguel", new DateOnly(2026, 3, 8), joaoHousingCategory));

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

    private sealed class ErrorResponse
    {
        public string? Message { get; init; }
    }
}
