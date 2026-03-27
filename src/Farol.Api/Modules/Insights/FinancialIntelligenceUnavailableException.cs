namespace Farol.Api.Modules.Insights;

public sealed class FinancialIntelligenceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
