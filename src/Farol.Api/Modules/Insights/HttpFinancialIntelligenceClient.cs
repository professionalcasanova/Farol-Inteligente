using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Farol.Api.Modules.Insights;

public sealed class HttpFinancialIntelligenceClient(
    HttpClient httpClient,
    IOptions<FinancialIntelligenceOptions> options) : IFinancialIntelligenceClient
{
    public async Task<FinancialAnalysisResponse> AnalyzeAsync(
        FinancialAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(
                options.Value.AnalyzePath,
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                throw new FinancialIntelligenceUnavailableException(
                    "The financial intelligence service returned an unsuccessful status code.");
            }

            var payload = await response.Content.ReadFromJsonAsync<FinancialAnalysisResponse>(cancellationToken);

            if (payload is null)
            {
                throw new FinancialIntelligenceUnavailableException(
                    "The financial intelligence service returned an empty response.");
            }

            return payload;
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new FinancialIntelligenceUnavailableException(
                "The financial intelligence service timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new FinancialIntelligenceUnavailableException(
                "The financial intelligence service is unavailable.",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new FinancialIntelligenceUnavailableException(
                "The financial intelligence service returned an unsupported payload.",
                exception);
        }
        catch (System.Text.Json.JsonException exception)
        {
            throw new FinancialIntelligenceUnavailableException(
                "The financial intelligence service returned an invalid payload.",
                exception);
        }
    }
}
