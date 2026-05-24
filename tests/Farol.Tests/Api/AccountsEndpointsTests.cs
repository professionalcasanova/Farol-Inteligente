using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Accounts;
using Farol.Api.Modules.Auth;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class AccountsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public AccountsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAccounts_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/accounts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Invalid access token.", error.Message);
    }

    [Fact]
    public async Task PostAccounts_ShouldCreateAccountForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Corrente",
            Type = Farol.Domain.Ledger.FinancialAccountType.BankAccount
        });

        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.NotNull(account);
        Assert.Equal("Conta Corrente", account.Name);
        Assert.Equal(Farol.Domain.Ledger.FinancialAccountType.BankAccount, account.Type);
        Assert.True(account.IsActive);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        Assert.Single(dbContext.FinancialAccounts);
    }

    [Fact]
    public async Task PostAccounts_CashType_ShouldCreateCashAccount()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Dinheiro",
            Type = FinancialAccountType.Cash
        });

        response.EnsureSuccessStatusCode();

        var account = await response.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.NotNull(account);
        Assert.Equal("Dinheiro", account.Name);
        Assert.Equal(FinancialAccountType.Cash, account.Type);
        Assert.True(account.IsActive);
    }

    [Fact]
    public async Task GetAccounts_ShouldReturnOnlyAccountsFromAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Maria",
            Type = Farol.Domain.Ledger.FinancialAccountType.BankAccount
        });

        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Joao",
            Type = Farol.Domain.Ledger.FinancialAccountType.Cash
        });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var response = await client.GetFromJsonAsync<List<AccountResponse>>("/api/accounts");

        Assert.NotNull(response);
        Assert.Single(response);
        Assert.Equal("Conta Maria", response[0].Name);
    }

    [Fact]
    public async Task PutAccounts_ShouldUpdateAuthenticatedUserAccount()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Antiga",
            Type = Farol.Domain.Ledger.FinancialAccountType.BankAccount
        });

        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(createdAccount);

        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{createdAccount.Id}", new UpdateAccountRequest
        {
            Name = "Conta Atualizada",
            Type = Farol.Domain.Ledger.FinancialAccountType.CreditCard,
            IsActive = false
        });

        updateResponse.EnsureSuccessStatusCode();

        var updatedAccount = await updateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.NotNull(updatedAccount);
        Assert.Equal("Conta Atualizada", updatedAccount.Name);
        Assert.Equal(Farol.Domain.Ledger.FinancialAccountType.CreditCard, updatedAccount.Type);
        Assert.False(updatedAccount.IsActive);
    }

    [Fact]
    public async Task PutAccounts_WithoutIsActive_ShouldKeepAccountActiveWhenChangingToCash()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Corrente",
            Type = FinancialAccountType.BankAccount
        });

        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(createdAccount);

        var updateResponse = await client.PutAsJsonAsync($"/api/accounts/{createdAccount.Id}", new
        {
            name = "Dinheiro",
            type = FinancialAccountType.Cash
        });

        updateResponse.EnsureSuccessStatusCode();

        var updatedAccount = await updateResponse.Content.ReadFromJsonAsync<AccountResponse>();

        Assert.NotNull(updatedAccount);
        Assert.Equal("Dinheiro", updatedAccount.Name);
        Assert.Equal(FinancialAccountType.Cash, updatedAccount.Type);
        Assert.True(updatedAccount.IsActive);
    }

    [Fact]
    public async Task DeleteAccounts_WithoutTransactions_ShouldDeleteOwnedAccount()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var createResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Dinheiro",
            Type = FinancialAccountType.Cash
        });

        var createdAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(createdAccount);

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{createdAccount.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Empty(dbContext.FinancialAccounts);
    }

    [Fact]
    public async Task DeleteAccounts_FromAnotherUser_ShouldReturnNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var createResponse = await client.PostAsJsonAsync("/api/accounts", new CreateAccountRequest
        {
            Name = "Conta Joao",
            Type = FinancialAccountType.Cash
        });

        var joaoAccount = await createResponse.Content.ReadFromJsonAsync<AccountResponse>();
        Assert.NotNull(joaoAccount);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{joaoAccount.Id}");

        Assert.Equal(HttpStatusCode.NotFound, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteAccounts_WithTransactions_ShouldReturnConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var accountId = await SeedAccountWithTransactionAsync("maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var deleteResponse = await client.DeleteAsync($"/api/accounts/{accountId}");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);

        var error = await deleteResponse.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("Financial account cannot be deleted because it has transactions.", error.Message);
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

    private async Task<Guid> SeedAccountWithTransactionAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var user = dbContext.Users.Single(user => user.Email == email);
        var account = new FinancialAccount(user.Id, "Dinheiro", FinancialAccountType.Cash);
        var category = Category.CreateSystem("Mercado", CategoryType.Expense);
        var transaction = new Transaction(
            account,
            TransactionType.Expense,
            10m,
            "Compra",
            new DateOnly(2026, 5, 13),
            category);

        dbContext.FinancialAccounts.Add(account);
        dbContext.Categories.Add(category);
        dbContext.Transactions.Add(transaction);
        await dbContext.SaveChangesAsync();

        return account.Id;
    }

    private sealed class ErrorResponse
    {
        public ApiError? Error { get; init; }
        public string? Message => Error?.Message;
    }

    private sealed class ApiError
    {
        public string? Message { get; init; }
    }
}
