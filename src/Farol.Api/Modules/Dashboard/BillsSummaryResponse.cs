namespace Farol.Api.Modules.Dashboard;

public sealed class BillsSummaryResponse
{
    public required decimal TotalPending { get; init; }
    public required decimal TotalOverdue { get; init; }
    public required decimal TotalPaid { get; init; }
    public required decimal PredictableTotal { get; init; }
    public required decimal RecurringTotal { get; init; }
    public required decimal InstallmentTotal { get; init; }
    public required int CountPending { get; init; }
    public required int CountOverdue { get; init; }
    public required int CountPaid { get; init; }
    public required int CountPredictable { get; init; }
    public required int CountRecurring { get; init; }
    public required int CountInstallment { get; init; }
    public required IReadOnlyList<BillsSummaryUpcomingResponse> Upcoming { get; init; }
}
