namespace Farol.Api.Modules.Insights;

public interface IFinancialIntelligenceClient
{
    Task<FinancialAnalysisResponse> AnalyzeAsync(
        FinancialAnalysisRequest request,
        CancellationToken cancellationToken);
}
