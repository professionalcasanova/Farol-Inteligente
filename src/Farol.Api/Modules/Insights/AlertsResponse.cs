namespace Farol.Api.Modules.Insights;

public sealed class AlertsResponse
{
    public required IReadOnlyList<AlertResponse> Alerts { get; init; }
}
