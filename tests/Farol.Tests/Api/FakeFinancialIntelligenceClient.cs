using Farol.Api.Modules.Insights;

namespace Farol.Tests.Api;

public sealed class FakeFinancialIntelligenceClient : IFinancialIntelligenceClient
{
    public FinancialAnalysisRequest? LastRequest { get; private set; }

    public Func<FinancialAnalysisRequest, CancellationToken, Task<FinancialAnalysisResponse>> Handler { get; set; }

    public FakeFinancialIntelligenceClient()
    {
        Handler = (_, _) => Task.FromResult(CreateHealthyResponse());
    }

    public Task<FinancialAnalysisResponse> AnalyzeAsync(
        FinancialAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Handler(request, cancellationToken);
    }

    public void Reset()
    {
        LastRequest = null;
        Handler = (_, _) => Task.FromResult(CreateHealthyResponse());
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
                Message = "Seu mÃªs estÃ¡ sob controle atÃ© aqui.",
                Cause = "VocÃª nÃ£o tem sinais fortes de pressÃ£o financeira imediata neste perÃ­odo.",
                Action = "Continue registrando o mÃªs para manter essa clareza."
            },
            Insights = [],
            RecommendedActions = []
        };
    }
}
