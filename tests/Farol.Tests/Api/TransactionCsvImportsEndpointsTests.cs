using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.Imports;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class TransactionCsvImportsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public TransactionCsvImportsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCsv_ShouldRequireAuthentication()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accountId = Guid.NewGuid();

        using var content = CreateMultipartContent(accountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-01,Salario,3000.00,Income,Salario
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostCsv_ShouldImportValidRowsForAuthenticatedUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Salario", CategoryType.Income),
            ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-01,Salario,3000.00,Income,Salario
            2026-03-02,Mercado,120.50,Expense,Alimentacao
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<ImportTransactionsCsvResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalRows);
        Assert.Equal(2, summary.ImportedRows);
        Assert.Equal(0, summary.SkippedRows);
        Assert.Empty(summary.Errors);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Equal(2, dbContext.Transactions.Count());
        Assert.All(dbContext.Transactions, transaction => Assert.Equal(seed.AccountId, transaction.FinancialAccountId));
    }

    [Fact]
    public async Task PostCsv_ShouldRejectAccountFromAnotherUser()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var joaoSeed = await SeedAccountAndCategoriesAsync("joao@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        using var content = CreateMultipartContent(joaoSeed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Mercado,120.50,Expense,Alimentacao
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCsv_MissingFile_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = new MultipartFormDataContent
        {
            { new StringContent(seed.AccountId.ToString()), "FinancialAccountId" }
        };

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("CSV file is required.", error.Message);
    }

    [Fact]
    public async Task PostCsv_EmptyFile_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, string.Empty);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("CSV file is required.", error.Message);
    }

    [Fact]
    public async Task PostCsv_HeaderOnly_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("CSV file must contain at least one data row.", error.Message);
    }

    [Fact]
    public async Task PostCsv_InvalidHeader_ReturnsBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            date,description,amount,type,category
            2026-03-02,Mercado,120.50,Expense,Alimentacao
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();

        Assert.NotNull(error);
        Assert.Equal("CSV header is invalid. Expected: occurredOn,description,amount,type,categoryName.", error.Message);
    }

    [Fact]
    public async Task PostCsv_ShouldSkipInvalidRowAndReturnError()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Mercado,120.50,Expense,Alimentacao
            2026-03-03,Restaurante,abc,Expense,
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<ImportTransactionsCsvResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalRows);
        Assert.Equal(1, summary.ImportedRows);
        Assert.Equal(1, summary.SkippedRows);
        Assert.Single(summary.Errors);
        Assert.Contains("invalid amount", summary.Errors[0].Message, StringComparison.OrdinalIgnoreCase);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Single(dbContext.Transactions);
    }

    [Fact]
    public async Task PostCsv_UnknownExplicitCategory_ReturnsOkWithSkippedRow()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Mercado,120.50,Expense,CategoriaInexistente
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<ImportTransactionsCsvResponse>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalRows);
        Assert.Equal(0, summary.ImportedRows);
        Assert.Equal(1, summary.SkippedRows);
        Assert.Single(summary.Errors);
        Assert.Contains("unknown category", summary.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostCsv_ShouldRespectValidCategoryName()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("Assinaturas", CategoryType.Expense),
            ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Mercado,59.90,Expense,Assinaturas
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var importedTransaction = dbContext.Transactions.Single();

        Assert.Equal(seed.CategoryIds["Assinaturas"], importedTransaction.CategoryId);
    }

    [Fact]
    public async Task PostCsv_ShouldCategorizeByRuleWhenCategoryNameIsEmpty()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Transporte", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Uber viagem,42.00,Expense,
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var importedTransaction = dbContext.Transactions.Single();

        Assert.Equal(seed.CategoryIds["Transporte"], importedTransaction.CategoryId);
    }

    [Fact]
    public async Task PostCsv_ShouldSkipIncompatibleTypeAndCategory()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Pagamento,120.00,Income,Alimentacao
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<ImportTransactionsCsvResponse>();

        Assert.NotNull(summary);
        Assert.Equal(1, summary.TotalRows);
        Assert.Equal(0, summary.ImportedRows);
        Assert.Equal(1, summary.SkippedRows);
        Assert.Single(summary.Errors);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Empty(dbContext.Transactions);
    }

    [Fact]
    public async Task PostCsv_BlankLines_ShouldSkipBlankRowsAndImportValidRows()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var seed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var content = CreateMultipartContent(seed.AccountId, """
            occurredOn,description,amount,type,categoryName

            2026-03-02,Mercado,120.50,Expense,Alimentacao

            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        var summary = await response.Content.ReadFromJsonAsync<ImportTransactionsCsvResponse>();

        Assert.NotNull(summary);
        Assert.Equal(2, summary.TotalRows);
        Assert.Equal(1, summary.ImportedRows);
        Assert.Equal(1, summary.SkippedRows);
        Assert.Single(summary.Errors);
        Assert.All(summary.Errors, error => Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PostCsv_ShouldNotMixDataBetweenUsers()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        await RegisterAndGetTokenAsync(client, "joao@email.com");
        var mariaSeed = await SeedAccountAndCategoriesAsync("maria@email.com", ("Alimentacao", CategoryType.Expense));
        await SeedAccountAndCategoriesAsync("joao@email.com", ("Alimentacao", CategoryType.Expense));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        using var content = CreateMultipartContent(mariaSeed.AccountId, """
            occurredOn,description,amount,type,categoryName
            2026-03-02,Mercado,120.50,Expense,Alimentacao
            """);

        var response = await client.PostAsync("/api/imports/transactions/csv", content);

        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();
        var mariaId = dbContext.Users.Single(user => user.Email == "maria@email.com").Id;
        var joaoId = dbContext.Users.Single(user => user.Email == "joao@email.com").Id;

        Assert.Single(dbContext.Transactions.Where(transaction => transaction.UserId == mariaId));
        Assert.Empty(dbContext.Transactions.Where(transaction => transaction.UserId == joaoId));
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

    private static MultipartFormDataContent CreateMultipartContent(Guid accountId, string csv)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");

        content.Add(new StringContent(accountId.ToString()), "FinancialAccountId");
        content.Add(fileContent, "File", "transactions.csv");

        return content;
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
