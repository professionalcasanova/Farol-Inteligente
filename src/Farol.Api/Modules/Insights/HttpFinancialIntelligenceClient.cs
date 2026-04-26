using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Farol.Api.Modules.Insights;

public sealed class HttpFinancialIntelligenceClient(
    HttpClient httpClient,
    IOptions<FinancialIntelligenceOptions> options,
    IConfiguration configuration) : IFinancialIntelligenceClient
{
    public async Task<FinancialAnalysisResponse> AnalyzeAsync(
        FinancialAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        var internalApiKey = configuration[options.Value.InternalApiKeyEnvironmentVariable];

        if (string.IsNullOrWhiteSpace(internalApiKey))
        {
            throw new InvalidOperationException(
                $"{options.Value.InternalApiKeyEnvironmentVariable} configuration is required.");
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, options.Value.AnalyzePath)
            {
                Content = JsonContent.Create(request)
            };
            httpRequest.Headers.Add(options.Value.InternalApiKeyHeaderName, internalApiKey);

            using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

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
