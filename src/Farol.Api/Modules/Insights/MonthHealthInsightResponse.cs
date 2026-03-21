namespace Farol.Api.Modules.Insights;

public sealed class MonthHealthInsightResponse
{
    public required string Type { get; init; }

    public required string Severity { get; init; }

    public required int Priority { get; init; }

    public required string Message { get; init; }

    public required string Cause { get; init; }

    public required string Action { get; init; }
}
