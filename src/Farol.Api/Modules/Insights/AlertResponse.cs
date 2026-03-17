namespace Farol.Api.Modules.Insights;

public sealed class AlertResponse
{
    public required string Type { get; init; }
    public required string Severity { get; init; }
    public required string Message { get; init; }
    public required decimal Amount { get; init; }
    public string? ActionUrl { get; init; }
}
