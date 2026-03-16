namespace Farol.Api.Modules.Dashboard;

public sealed class MonthlySummaryResponse
{
    public required int Month { get; init; }
    public required int Year { get; init; }
    public required decimal TotalIncome { get; init; }
    public required decimal TotalExpense { get; init; }
    public required decimal Balance { get; init; }
    public required IReadOnlyList<MonthlySummaryCategoryResponse> ByCategory { get; init; }
}
