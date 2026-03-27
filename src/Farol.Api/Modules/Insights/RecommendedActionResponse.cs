namespace Farol.Api.Modules.Insights;

public sealed class RecommendedActionResponse
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public required string Target { get; init; }
}
