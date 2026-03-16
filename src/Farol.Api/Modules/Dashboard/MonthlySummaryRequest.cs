using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Dashboard;

public sealed class MonthlySummaryRequest
{
    [Range(1, 12)]
    public int Month { get; init; }

    [Range(2000, 2100)]
    public int Year { get; init; }
}
