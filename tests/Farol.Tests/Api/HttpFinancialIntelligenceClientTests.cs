using System.Net;
using System.Net.Http.Json;
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

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new FinancialAnalysisResponse
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
                })
            };
        });

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

        var client = new HttpFinancialIntelligenceClient(
            httpClient,
            Options.Create(new FinancialIntelligenceOptions()),
            configuration);

        await client.AnalyzeAsync(new FinancialAnalysisRequest
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
            Categories = []
        }, CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.True(capturedRequest.Headers.TryGetValues("X-Farol-Internal-Key", out var values));
        Assert.Equal("test-internal-key", Assert.Single(values));
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(responder(request));
        }
    }
}
