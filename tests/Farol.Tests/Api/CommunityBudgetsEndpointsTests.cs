using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Farol.Api.Modules.Auth;
using Farol.Api.Modules.CommunityBudgets;
using Farol.Domain.Budgets;
using Farol.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Farol.Tests.Api;

public sealed class CommunityBudgetsEndpointsTests : IClassFixture<FarolApiFactory>
{
    private readonly FarolApiFactory _factory;

    public CommunityBudgetsEndpointsTests(FarolApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostCommunityBudgets_WithoutAuthentication_ShouldReturnUnauthorized()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgets_PublicBudgetWithItems_ShouldCreateBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());

        response.EnsureSuccessStatusCode();

        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(response);

        Assert.NotEqual(Guid.Empty, budget.Id);
        Assert.Equal("Orcamento familia enxuto", budget.Title);
        Assert.Equal("Casal com um filho", budget.TargetProfile);
        Assert.True(budget.IsPublic);
        Assert.Equal("published", budget.Status);
        Assert.Equal(0, budget.ReportCount);
        Assert.Equal(4200m, budget.MonthlyIncomeReference);
        Assert.Equal(2, budget.Items.Count);
        Assert.Contains(budget.Items, item =>
            item.Name == "Aluguel" &&
            item.Type == "expense" &&
            item.AllocationType == "fixed_amount" &&
            item.Amount == 1200m &&
            item.Percentage is null);
        Assert.Contains(budget.Items, item =>
            item.Name == "Reserva" &&
            item.Type == "reserve" &&
            item.AllocationType == "percentage" &&
            item.Amount is null &&
            item.Percentage == 10m);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FarolDbContext>();

        Assert.Single(dbContext.CommunityBudgets);
        Assert.Equal(2, dbContext.CommunityBudgetItems.Count());
    }

    [Fact]
    public async Task GetCommunityBudgets_ShouldListOnlyPublicBudgets()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(title: "Publico Maria", isPublic: true));
        await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(title: "Privado Maria", isPublic: false));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.GetAsync("/api/community-budgets");

        response.EnsureSuccessStatusCode();

        var budgets = await ApiTestResponseReader.ReadDataAsync<List<CommunityBudgetSummaryResponse>>(response);

        Assert.Single(budgets);
        Assert.Equal("Publico Maria", budgets[0].Title);
        Assert.True(budgets[0].IsPublic);
        Assert.Equal("published", budgets[0].Status);
        Assert.Equal(0, budgets[0].ReportCount);
    }

    [Fact]
    public async Task GetCommunityBudgets_ReportedBudget_ShouldNotListBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(title: "Publico Maria"));
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var reportResponse = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest());
        reportResponse.EnsureSuccessStatusCode();

        var listResponse = await client.GetAsync("/api/community-budgets");

        listResponse.EnsureSuccessStatusCode();

        var budgets = await ApiTestResponseReader.ReadDataAsync<List<CommunityBudgetSummaryResponse>>(listResponse);

        Assert.Empty(budgets);
    }

    [Fact]
    public async Task GetCommunityBudget_PrivateBudgetFromAnotherUser_ShouldReturnNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(isPublic: false));
        var privateBudget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.GetAsync($"/api/community-budgets/{privateBudget.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutCommunityBudget_FromAnotherUser_ShouldReturnNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.PutAsJsonAsync($"/api/community-budgets/{budget.Id}", ValidRequest(title: "Alterado"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutCommunityBudget_OwnBudget_ShouldUpdateBudgetAndItems()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        var updateResponse = await client.PutAsJsonAsync($"/api/community-budgets/{budget.Id}", ValidRequest(
            title: "Orcamento atualizado",
            items:
            [
                new CommunityBudgetItemRequest
                {
                    Name = "Mercado",
                    CategoryName = "Alimentacao",
                    Type = "expense",
                    AllocationType = "fixed_amount",
                    Amount = 900m,
                    SortOrder = 1
                }
            ]));

        updateResponse.EnsureSuccessStatusCode();

        var updated = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(updateResponse);

        Assert.Equal("Orcamento atualizado", updated.Title);
        Assert.Single(updated.Items);
        Assert.Equal("Mercado", updated.Items[0].Name);
        Assert.Equal(900m, updated.Items[0].Amount);
    }

    [Fact]
    public async Task DeleteCommunityBudget_OwnBudget_ShouldRemoveBudget()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        var deleteResponse = await client.DeleteAsync($"/api/community-budgets/{budget.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/community-budgets/{budget.Id}");

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task PostImportCommunityBudget_PublicBudget_ShouldCreatePrivateIndependentCopy()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(title: "Modelo Joao"));
        var source = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var importResponse = await client.PostAsync($"/api/community-budgets/{source.Id}/import", null);

        importResponse.EnsureSuccessStatusCode();

        var imported = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(importResponse);

        Assert.NotEqual(source.Id, imported.Id);
        Assert.Equal("Modelo Joao", imported.Title);
        Assert.False(imported.IsPublic);
        Assert.Equal("draft", imported.Status);
        Assert.Equal(2, imported.Items.Count);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var updateSourceResponse = await client.PutAsJsonAsync($"/api/community-budgets/{source.Id}", ValidRequest(
            title: "Modelo Joao editado",
            items:
            [
                new CommunityBudgetItemRequest
                {
                    Name = "Aluguel alterado",
                    CategoryName = "Moradia",
                    Type = "expense",
                    AllocationType = "fixed_amount",
                    Amount = 2000m,
                    SortOrder = 1
                }
            ]));
        updateSourceResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var importedResponse = await client.GetAsync($"/api/community-budgets/{imported.Id}");
        var importedAfterSourceUpdate = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(importedResponse);

        Assert.Equal("Modelo Joao", importedAfterSourceUpdate.Title);
        Assert.Equal(2, importedAfterSourceUpdate.Items.Count);
        Assert.Contains(importedAfterSourceUpdate.Items, item => item.Name == "Aluguel" && item.Amount == 1200m);
    }

    [Fact]
    public async Task PostImportCommunityBudget_PrivateBudgetFromAnotherUser_ShouldReturnNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(isPublic: false));
        var privateBudget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);

        var importResponse = await client.PostAsync($"/api/community-budgets/{privateBudget.Id}/import", null);

        Assert.Equal(HttpStatusCode.NotFound, importResponse.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgetReport_PublicBudget_ShouldCreateReportAndMarkBudgetReported()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(title: "Publico Maria"));
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);

        var response = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest(reason: "sensitive_data", description: "Contem contato pessoal."));

        response.EnsureSuccessStatusCode();

        var report = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetReportResponse>(response);

        Assert.NotEqual(Guid.Empty, report.Id);
        Assert.Equal(budget.Id, report.CommunityBudgetId);
        Assert.Equal("sensitive_data", report.Reason);
        Assert.Equal("Contem contato pessoal.", report.Description);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var getResponse = await client.GetAsync($"/api/community-budgets/{budget.Id}");
        var reportedBudget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(getResponse);

        Assert.Equal("reported", reportedBudget.Status);
        Assert.False(reportedBudget.IsPublic);
        Assert.Equal(1, reportedBudget.ReportCount);
    }

    [Fact]
    public async Task PostCommunityBudgetReport_DuplicateReport_ShouldReturnConflict()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var firstResponse = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest());
        firstResponse.EnsureSuccessStatusCode();

        var duplicateResponse = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest(reason: "spam"));

        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgetReport_OwnBudget_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        var response = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgetReport_PrivateBudget_ShouldReturnNotFound()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(isPublic: false));
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var response = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PutCommunityBudget_ReportedOwnBudget_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var mariaToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var joaoToken = await RegisterAndGetTokenAsync(client, "joao@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var createResponse = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest());
        var budget = await ApiTestResponseReader.ReadDataAsync<CommunityBudgetResponse>(createResponse);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", joaoToken);
        var reportResponse = await client.PostAsJsonAsync(
            $"/api/community-budgets/{budget.Id}/reports",
            ValidReportRequest());
        reportResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", mariaToken);
        var updateResponse = await client.PutAsJsonAsync(
            $"/api/community-budgets/{budget.Id}",
            ValidRequest(title: "Tentativa de editar"));

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgets_PublicBudgetWithoutItems_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(items: []));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgets_InvalidPercentage_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(items:
        [
            new CommunityBudgetItemRequest
            {
                Name = "Reserva",
                CategoryName = "Reserva",
                Type = "reserve",
                AllocationType = "percentage",
                Percentage = 120m,
                SortOrder = 1
            }
        ]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgets_SensitiveData_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync("/api/community-budgets", ValidRequest(
            description: "Contato maria@email.com para detalhes"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostCommunityBudgets_HiddenStatus_ShouldReturnBadRequest()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(
            "/api/community-budgets",
            ValidRequest(status: "hidden"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CommunityBudgetRequest ValidRequest(
        string title = "Orcamento familia enxuto",
        string description = "Modelo simples para organizar gastos essenciais.",
        string targetProfile = "Casal com um filho",
        bool isPublic = true,
        string? status = null,
        IReadOnlyList<CommunityBudgetItemRequest>? items = null)
    {
        return new CommunityBudgetRequest
        {
            Title = title,
            Description = description,
            TargetProfile = targetProfile,
            MonthlyIncomeReference = 4200m,
            IsPublic = isPublic,
            Status = status,
            Items = items ??
            [
                new CommunityBudgetItemRequest
                {
                    Name = "Aluguel",
                    CategoryName = "Moradia",
                    Type = "expense",
                    AllocationType = "fixed_amount",
                    Amount = 1200m,
                    SortOrder = 1
                },
                new CommunityBudgetItemRequest
                {
                    Name = "Reserva",
                    CategoryName = "Reserva",
                    Type = "reserve",
                    AllocationType = "percentage",
                    Percentage = 10m,
                    Notes = "Guardar no inicio do mes.",
                    SortOrder = 2
                }
            ]
        };
    }

    private static CommunityBudgetReportRequest ValidReportRequest(
        string reason = "misleading",
        string? description = "Informacoes do modelo parecem incorretas.")
    {
        return new CommunityBudgetReportRequest
        {
            Reason = reason,
            Description = description
        };
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
