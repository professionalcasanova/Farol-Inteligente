namespace Farol.Api.Modules.Insights;

public sealed class MonthHealthSummaryResponse
{
    public required string Message { get; init; }

    public required string Cause { get; init; }

    public required string Action { get; init; }
}
