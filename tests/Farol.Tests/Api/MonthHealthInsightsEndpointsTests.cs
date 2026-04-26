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
        var today = _factory.Today;

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            Task.FromResult(new FinancialAnalysisResponse
            {
                ContractVersion = "v1",
                Status = "critical",
                Score = 32,
                Message = "CrÃ­tico: saldo negativo e dÃ­vidas atrasadas.",
                Reasons = new[] { "Conta de energia vencida hÃ¡ 10 dias", "Saldo do mÃªs estÃ¡ em -R$ 1.200,00" },
                Actions = new[] { "Priorize o pagamento das contas fixas vencidas", "Suspenda despesas nÃ£o essenciais atÃ© estabilizar o saldo" },
                Priority = 110,
                Summary = new FinancialAnalysisSummaryResponse
                {
                    Message = "HÃ¡ contas vencidas no seu mÃªs.",
                    Cause = "VocÃª tem contas que jÃ¡ passaram do vencimento e isso aumenta a pressÃ£o financeira agora.",
                    Action = "Priorize quitar ou renegociar as contas vencidas hoje."
                },
                Insights = new List<FinancialAnalysisInsightResponse>
                {
                    new FinancialAnalysisInsightResponse
                    {
                        Type = "overdue_bills",
                        Severity = "high",
                        Priority = 100,
                        Message = "HÃ¡ contas vencidas no seu mÃªs.",
                        Cause = "VocÃª tem contas que jÃ¡ passaram do vencimento e isso aumenta a pressÃ£o financeira agora.",
                        Action = "Priorize quitar ou renegociar as contas vencidas hoje."
                    }
                },
                RecommendedActions = new List<FinancialAnalysisRecommendedActionResponse>
                {
                    new FinancialAnalysisRecommendedActionResponse
                    {
                        Id = "review_overdue_bills",
                        Label = "Resolver contas vencidas",
                        Target = "/bills?status=overdue"
                    }
                }
            });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal(32, response.Score);
        Assert.Equal("CrÃ­tico: saldo negativo e dÃ­vidas atrasadas.", response.Message);
        Assert.Equal(2, response.Reasons.Count);
        Assert.Contains(response.Reasons, reason => reason.Contains("Conta de energia vencida", StringComparison.Ordinal));
        Assert.Equal(2, response.Actions.Count);
        Assert.Contains(response.Actions, action => action.Contains("Priorize o pagamento das contas fixas vencidas", StringComparison.Ordinal));
        Assert.Equal(110, response.Priority);
        Assert.Equal("HÃ¡ contas vencidas no seu mÃªs.", response.Summary.Message);
        Assert.Equal("overdue_bills", Assert.Single(response.Insights).Type);
        Assert.Equal("review_overdue_bills", Assert.Single(response.RecommendedActions).Id);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldProxyFinancialIntelligenceResponse_ForNegativeBalanceAndHighVariableExpenses()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            Task.FromResult(new FinancialAnalysisResponse
            {
                ContractVersion = "v1",
                Status = "critical",
                Score = 18,
                Message = "Criticidade de fluxo de caixa detectada.",
                Reasons = new[] { "Saldo do mÃªs estÃ¡ em -R$ 1.200,00", "Despesas variÃ¡veis > 80%" },
                Actions = new[] { "Suspenda despesas nÃ£o essenciais atÃ© estabilizar o saldo" },
                Priority = 105,
                Summary = new FinancialAnalysisSummaryResponse
                {
                    Message = "VocÃª estÃ¡ no vermelho e tem pressÃ£o de gastos variÃ¡veis.",
                    Cause = "Gastos elevados versus renda e cashflow negativo.",
                    Action = "Ajuste imediatamente o orÃ§amento." 
                },
                Insights = new List<FinancialAnalysisInsightResponse>
                {
                    new FinancialAnalysisInsightResponse
                    {
                        Type = "negative_balance_high_variable_expense",
                        Severity = "high",
                        Priority = 105,
                        Message = "Saldo negativo e mais de 80% das despesas sÃ£o variÃ¡veis.",
                        Cause = "Saldo do mÃªs estÃ¡ em -R$ 1.200,00 e despesas variÃ¡veis sÃ£o muito altas.",
                        Action = "Suspender despesas nÃ£o essenciais e renegociar contrato."
                    }
                },
                RecommendedActions = new List<FinancialAnalysisRecommendedActionResponse>
                {
                    new FinancialAnalysisRecommendedActionResponse
                    {
                        Id = "suspend_non_essential",
                        Label = "Suspender nÃ£o essenciais",
                        Target = "/transactions"
                    }
                }
            });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal(18, response.Score);
        Assert.Equal("Criticidade de fluxo de caixa detectada.", response.Message);
        Assert.Contains("Saldo do mÃªs estÃ¡ em -R$ 1.200,00", response.Reasons);
        Assert.Contains("Suspenda despesas nÃ£o essenciais atÃ© estabilizar o saldo", response.Actions);
        Assert.Equal(105, response.Priority);
        Assert.Equal("negative_balance_high_variable_expense", Assert.Single(response.Insights).Type);
        Assert.Equal("suspend_non_essential", Assert.Single(response.RecommendedActions).Id);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldPreserveExplicitNegativeSummaryFromFinancialIntelligenceResponse()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            Task.FromResult(new FinancialAnalysisResponse
            {
                ContractVersion = "v1",
                Status = "critical",
                Score = 22,
                Message = "Seu dinheiro livre ficou negativo neste mes.",
                Reasons = new[] { "Saldo do mes esta em -R$ 480,00" },
                Actions = new[] { "Pause novos gastos ajustaveis e reorganize o restante do mes" },
                Priority = 100,
                Summary = new FinancialAnalysisSummaryResponse
                {
                    Message = "Seu dinheiro livre ficou negativo neste mes.",
                    Cause = "Depois dos gastos e compromissos do mes, seu dinheiro livre ficou negativo e voce corre risco de nao conseguir bancar o restante do periodo sem ajuste.",
                    Action = "Pause novos gastos ajustaveis e revise as maiores saidas do mes para decidir o que pode ser reduzido ou adiado agora."
                },
                Insights = new List<FinancialAnalysisInsightResponse>
                {
                    new FinancialAnalysisInsightResponse
                    {
                        Type = "negative_free_money",
                        Severity = "high",
                        Priority = 90,
                        Message = "Seu dinheiro livre ficou negativo neste mes.",
                        Cause = "Depois dos gastos e do que ainda esta reservado, faltou folga no mes.",
                        Action = "Evite novos gastos agora e revise as maiores saidas do periodo."
                    }
                },
                RecommendedActions = new List<FinancialAnalysisRecommendedActionResponse>
                {
                    new FinancialAnalysisRecommendedActionResponse
                    {
                        Id = "review_expenses",
                        Label = "Ver saidas do mes",
                        Target = "/transactions"
                    }
                }
            });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("critical", response.Status);
        Assert.Equal("Seu dinheiro livre ficou negativo neste mes.", response.Message);
        Assert.Equal("Seu dinheiro livre ficou negativo neste mes.", response.Summary.Message);
        Assert.Contains("dinheiro livre ficou negativo", response.Summary.Cause, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("baixo", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("negative_free_money", Assert.Single(response.Insights).Type);
        Assert.Equal("review_expenses", Assert.Single(response.RecommendedActions).Id);
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
        Assert.Equal(-690m, request.Totals.FreeToSpend);
        Assert.Equal(1, request.Bills.OverdueCount);
        Assert.Equal(300m, request.Bills.OverdueAmount);
        Assert.Equal(3, request.Bills.PendingCount);
        Assert.Equal(390m, request.Bills.PendingAmount);
        Assert.Equal(3, request.Bills.Upcoming7DaysCount);
        Assert.Equal(390m, request.Bills.Upcoming7DaysAmount);
        Assert.Equal(3, request.Bills.MaxOverdueDays);
    }

    [Fact]
    public async Task GetMonthHealth_FutureMonth_ShouldReturnProjectionWithoutCallingFinancialIntelligenceService()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var nextMonthStart = new DateOnly(_factory.Today.Year, _factory.Today.Month, 1).AddMonths(1);

        await SeedSeriesAsync(
            "maria@email.com",
            "Internet fibra",
            160m,
            nextMonthStart,
            BillSeries.RecurringKind,
            12);
        await SeedSeriesAsync(
            "maria@email.com",
            "Notebook",
            500m,
            nextMonthStart,
            BillSeries.InstallmentKind,
            10);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={nextMonthStart.Month}&year={nextMonthStart.Year}");

        Assert.NotNull(response);
        Assert.Equal("attention", response.Status);
        Assert.Contains("projecao", response.Message ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(response.Actions, item => item.Contains("Confirme as entradas esperadas", StringComparison.Ordinal));
        Assert.Contains(response.RecommendedActions, item => item.Id == "review_next_bills");
        Assert.Null(_factory.FinancialIntelligenceClient.LastRequest);
    }

    [Fact]
    public async Task GetMonthHealth_ShouldUsePlannedBudgetRemainingWhenBuildingSnapshot()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("SalÃ¡rio", CategoryType.Income),
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
                (today, "SalÃ¡rio", 16000m, TransactionType.Income, seed.CategoryIds["SalÃ¡rio"]),
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
        var today = _factory.Today;
        var seed = await SeedAccountAndCategoriesAsync(
            "maria@email.com",
            ("SalÃ¡rio", CategoryType.Income),
            ("Delivery", CategoryType.Expense),
            ("Mercado", CategoryType.Expense));

        await SeedTransactionsAsync(
            "maria@email.com",
            seed.AccountId,
            [
                (today, "SalÃ¡rio", 5000m, TransactionType.Income, seed.CategoryIds["SalÃ¡rio"]),
                (today, "AlmoÃ§o", 100m, TransactionType.Expense, seed.CategoryIds["Delivery"]),
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
            item.Name == "SalÃ¡rio" &&
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
        var today = _factory.Today;

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
        var today = _factory.Today;

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            throw new FinancialIntelligenceUnavailableException("Service unavailable.");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync($"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(payload);
        Assert.Equal("A inteligÃªncia financeira estÃ¡ indisponÃ­vel no momento.", payload.Message);
    }

    [Fact]
    public async Task GetMonthHealth_WithHealthyPayloadWithoutInsights_ShouldUseSummaryFallbacks()
    {
        await _factory.ResetDatabaseAsync();
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndGetTokenAsync(client, "maria@email.com");
        var today = _factory.Today;

        _factory.FinancialIntelligenceClient.Handler = (_, _) =>
            Task.FromResult(new FinancialAnalysisResponse
            {
                ContractVersion = "v1",
                Status = "healthy",
                Score = 100,
                Summary = new FinancialAnalysisSummaryResponse
                {
                    Message = "Seu mÃƒÂªs estÃƒÂ¡ sob controle atÃƒÂ© aqui.",
                    Cause = "VocÃƒÂª nÃƒÂ£o tem sinais fortes de pressÃƒÂ£o financeira imediata neste perÃƒÂ­odo.",
                    Action = "Continue registrando o mÃƒÂªs para manter essa clareza."
                },
                Insights = [],
                RecommendedActions = []
            });

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetFromJsonAsync<MonthHealthResponse>(
            $"/api/insights/month-health?month={today.Month}&year={today.Year}");

        Assert.NotNull(response);
        Assert.Equal("healthy", response.Status);
        Assert.Equal(100, response.Score);
        Assert.Equal("Seu mÃƒÂªs estÃƒÂ¡ sob controle atÃƒÂ© aqui.", response.Message);
        Assert.Equal(["VocÃƒÂª nÃƒÂ£o tem sinais fortes de pressÃƒÂ£o financeira imediata neste perÃƒÂ­odo."], response.Reasons);
        Assert.Equal(["Continue registrando o mÃƒÂªs para manter essa clareza."], response.Actions);
        Assert.Equal(10, response.Priority);
        Assert.Empty(response.Insights);
        Assert.Empty(response.RecommendedActions);
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
            Password = "Password123"
        });

        response.EnsureSuccessStatusCode();

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        Assert.NotNull(authResponse);

        return authResponse.AccessToken;
    }
}
