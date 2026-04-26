namespace Farol.Api.Modules.Insights;

public sealed class FinancialIntelligenceOptions
{
    public const string SectionName = "FinancialIntelligence";

    public string BaseUrl { get; init; } = "http://127.0.0.1:8000";

    public string AnalyzePath { get; init; } = "/analyze/v1";

    public int TimeoutSeconds { get; init; } = 3;

    public string InternalApiKeyEnvironmentVariable { get; init; } = "FAROL_INTERNAL_API_KEY";

    public string InternalApiKeyHeaderName { get; init; } = "X-Farol-Internal-Key";

    public string ContractVersion { get; init; } = "v1";

    public string Currency { get; init; } = "BRL";
}
