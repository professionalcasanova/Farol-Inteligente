using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Transactions;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class TransactionsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public TransactionsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetTransactions_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/transactions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostTransactions_ShouldCreateTransactionForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedOwnedAccountAndSystemCategoryAsync("maria@email.com", "Conta Corrente", "Moradia", CategoryType.Expense);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            FinancialAccountId = seed.AccountId,
            CategoryId = seed.CategoryId,
            Type = TransactionType.Expense,
            Amount = 150.75m,
            Description = "Aluguel",
            OccurredOn = new DateOnly(2026, 3, 16)
        });

        response.EnsureSuccessStatusCode();

        var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();

        Assert.NotNull(transaction);
        Assert.Equal(seed.AccountId, transaction.FinancialAccountId);
        Assert.Equal(seed.CategoryId, transaction.CategoryId);
        Assert.Equal(TransactionType.Expense, transaction.Type);
        Assert.Equal(150.75m, transaction.Amount);
    }

    [Fact]
    public async Task PostTransactions_ShouldAcceptDecimalAmountUnderPtBrCulture()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var originalCulture = CultureInfo.DefaultThreadCurrentCulture;
        var originalUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var ptBr = new CultureInfo("pt-BR");

        CultureInfo.DefaultThreadCurrentCulture = ptBr;
        CultureInfo.DefaultThreadCurrentUICulture = ptBr;

        try
        {
            var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
            var seed = await SeedOwnedAccountAndSystemCategoryAsync(
                "maria@email.com",
                "Conta Corrente",
                "Salario",
                CategoryType.Income);

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
            {
                FinancialAccountId = seed.AccountId,
                CategoryId = seed.CategoryId,
                Type = TransactionType.Income,
                Amount = 0.01m,
                Description = "Ajuste",
                OccurredOn = new DateOnly(2026, 3, 16)
            });

            response.EnsureSuccessStatusCode();

            var transaction = await response.Content.ReadFromJsonAsync<TransactionResponse>();

            Assert.NotNull(transaction);
            Assert.Equal(0.01m, transaction.Amount);
        }
        finally
        {
            CultureInfo.DefaultThreadCurrentCulture = originalCulture;
            CultureInfo.DefaultThreadCurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public async Task PostTransactions_ShouldRejectIncompatibleCategoryType()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedOwnedAccountAndSystemCategoryAsync("maria@email.com", "Conta Corrente", "Salario", CategoryType.Income);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            FinancialAccountId = seed.AccountId,
            CategoryId = seed.CategoryId,
            Type = TransactionType.Expense,
            Amount = 100m,
            Description = "Mercado",
            OccurredOn = new DateOnly(2026, 3, 16)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetTransactions_ShouldReturnOnlyTransactionsFromAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var mariaSeed = await SeedOwnedAccountAndSystemCategoryAsync("maria@email.com", "Conta Maria", "Moradia", CategoryType.Expense);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            FinancialAccountId = mariaSeed.AccountId,
            CategoryId = mariaSeed.CategoryId,
            Type = TransactionType.Expense,
            Amount = 200m,
            Description = "Conta Maria",
            OccurredOn = new DateOnly(2026, 3, 10)
        });

        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoSeed = await SeedOwnedAccountAndSystemCategoryAsync("joao@email.com", "Conta Joao", "Salario", CategoryType.Income);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            FinancialAccountId = joaoSeed.AccountId,
            CategoryId = joaoSeed.CategoryId,
            Type = TransactionType.Income,
            Amount = 3000m,
            Description = "Conta Joao",
            OccurredOn = new DateOnly(2026, 3, 12)
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var transactions = await client.GetFromJsonAsync<List<TransactionResponse>>("/api/transactions");

        Assert.NotNull(transactions);
        Assert.Single(transactions);
        Assert.Equal("Conta Maria", transactions[0].Description);
    }

    [Fact]
    public async Task PutTransactions_ShouldUpdateOwnedTransaction()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var expenseSeed = await SeedOwnedAccountAndSystemCategoryAsync("maria@email.com", "Conta Corrente", "Moradia", CategoryType.Expense);
        var incomeCategoryId = await SeedVisibleCategoryAsync("maria@email.com", "Salario", CategoryType.Income);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest
        {
            FinancialAccountId = expenseSeed.AccountId,
            CategoryId = expenseSeed.CategoryId,
            Type = TransactionType.Expense,
            Amount = 80m,
            Description = "Mercado",
            OccurredOn = new DateOnly(2026, 3, 14)
        });

        createResponse.EnsureSuccessStatusCode();

        var createdTransaction = await createResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        Assert.NotNull(createdTransaction);

        var updateResponse = await client.PutAsJsonAsync($"/api/transactions/{createdTransaction.Id}", new UpdateTransactionRequest
        {
            FinancialAccountId = expenseSeed.AccountId,
            CategoryId = incomeCategoryId,
            Type = TransactionType.Income,
            Amount = 500m,
            Description = "Reembolso",
            OccurredOn = new DateOnly(2026, 3, 15)
        });

        updateResponse.EnsureSuccessStatusCode();

        var updatedTransaction = await updateResponse.Content.ReadFromJsonAsync<TransactionResponse>();

        Assert.NotNull(updatedTransaction);
        Assert.Equal(TransactionType.Income, updatedTransaction.Type);
        Assert.Equal(incomeCategoryId, updatedTransaction.CategoryId);
        Assert.Equal("Reembolso", updatedTransaction.Description);
        Assert.Equal(500m, updatedTransaction.Amount);
    }

    private async Task<(Guid AccountId, Guid CategoryId)> SeedOwnedAccountAndSystemCategoryAsync(
        string email,
        string accountName,
        string categoryName,
        CategoryType categoryType)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var account = new FinancialAccount(userId, accountName, FinancialAccountType.BankAccount);
        var category = Category.CreateSystem(categoryName, categoryType);

        dbContext.FinancialAccounts.Add(account);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        return (account.Id, category.Id);
    }

    private async Task<Guid> SeedVisibleCategoryAsync(string email, string categoryName, CategoryType categoryType)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var userId = dbContext.Users.Single(user => user.Email == email).Id;
        var category = Category.CreateSystem(categoryName, categoryType);

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        return category.Id;
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
