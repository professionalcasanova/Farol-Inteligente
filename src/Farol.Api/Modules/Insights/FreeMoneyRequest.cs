using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Insights;

public sealed class FreeMoneyRequest
{
    [Range(1, 12)]
    public int Month { get; init; }

    [Range(2000, 2100)]
    public int Year { get; init; }
}
