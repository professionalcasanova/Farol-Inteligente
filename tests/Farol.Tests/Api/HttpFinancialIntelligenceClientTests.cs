using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Farol.Api.Modules.Insights;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Farol.Tests.Api;

public sealed class HttpFinancialIntelligenceClientTests
{
    [Fact]
    public async Task AnalyzeAsync_ConfiguredInternalApiKey_ShouldSendHeader()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateHealthyResponse())
            });
        });

        var client = CreateClient(handler);

        await client.AnalyzeAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.TryGetValues("X-Internal-API-Key", out var values));
        Assert.Equal("test-internal-key", Assert.Single(values));
    }

    [Fact]
    public async Task AnalyzeAsync_Request_ShouldUsePythonContractFieldNames()
    {
        string? capturedJson = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedJson = await request.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(CreateHealthyResponse())
            };
        });

        var client = CreateClient(handler);

        await client.AnalyzeAsync(CreateRequest(), CancellationToken.None);

        Assert.NotNull(capturedJson);
        using var document = JsonDocument.Parse(capturedJson);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("contractVersion", out _));
        Assert.True(root.TryGetProperty("reference", out var reference));
        Assert.True(reference.TryGetProperty("userId", out _));
        Assert.True(root.TryGetProperty("plannedBudget", out _) is false);
        Assert.True(root.GetProperty("totals").TryGetProperty("plannedBudget", out _));
        Assert.True(root.GetProperty("bills").TryGetProperty("upcoming7DaysAmount", out _));
        Assert.True(root.GetProperty("categories")[0].TryGetProperty("categoryId", out _));
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidPayload_ShouldThrowUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    contractVersion = "v1",
                    status = "healthy"
                })
            }));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<FinancialIntelligenceUnavailableException>(() =>
            client.AnalyzeAsync(CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task AnalyzeAsync_UnsuccessfulStatusCode_ShouldThrowUnavailableException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var client = CreateClient(handler);

        await Assert.ThrowsAsync<FinancialIntelligenceUnavailableException>(() =>
            client.AnalyzeAsync(CreateRequest(), CancellationToken.None));
    }

    private static HttpFinancialIntelligenceClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://127.0.0.1:8000")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FAROL_INTERNAL_API_KEY"] = "test-internal-key"
            })
            .Build();

        return new HttpFinancialIntelligenceClient(
            httpClient,
            Options.Create(new FinancialIntelligenceOptions()),
            configuration);
    }

    private static FinancialAnalysisRequest CreateRequest()
    {
        return new FinancialAnalysisRequest
        {
            ContractVersion = "v1",
            Reference = new FinancialAnalysisReferenceRequest
            {
                UserId = "user-1",
                Month = 3,
                Year = 2026,
                Currency = "BRL"
            },
            Totals = new FinancialAnalysisTotalsRequest
            {
                Income = 1000,
                Expense = 500,
                Balance = 500,
                PlannedBudget = 500,
                BudgetSpent = 400,
                BudgetRemaining = 100,
                FreeToSpend = 500
            },
            Bills = new FinancialAnalysisBillsRequest
            {
                PendingAmount = 0,
                OverdueAmount = 0,
                PendingCount = 0,
                OverdueCount = 0,
                Upcoming7DaysAmount = 0,
                Upcoming7DaysCount = 0,
                MaxOverdueDays = 0,
                PredictableAmount = 0,
                PredictableCount = 0,
                RecurringAmount = 0,
                RecurringCount = 0,
                InstallmentAmount = 0,
                InstallmentCount = 0
            },
            Categories =
            [
                new FinancialAnalysisCategoryRequest
                {
                    CategoryId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                    Name = "Mercado",
                    Type = "expense",
                    Amount = 250
                }
            ]
        };
    }

    private static FinancialAnalysisResponse CreateHealthyResponse()
    {
        return new FinancialAnalysisResponse
        {
            ContractVersion = "v1",
            Status = "healthy",
            Score = 100,
            Summary = new FinancialAnalysisSummaryResponse
            {
                Message = "ok",
                Cause = "ok",
                Action = "ok"
            },
            Insights = [],
            RecommendedActions = []
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return responder(request);
        }
    }
}
