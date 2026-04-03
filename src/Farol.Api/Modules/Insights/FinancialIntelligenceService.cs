using Farol.Domain.Ledger;
using Microsoft.Extensions.Options;

namespace Farol.Api.Modules.Insights;

public sealed class FinancialIntelligenceService(
    MonthlyInsightsService monthlyInsightsService,
    IFinancialIntelligenceClient financialIntelligenceClient,
    IOptions<FinancialIntelligenceOptions> options)
{
    public async Task<MonthHealthResponse> GetMonthHealthAsync(
        Guid userId,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        var snapshot = await monthlyInsightsService.GetMonthlySnapshotAsync(
            userId,
            periodStart,
            cancellationToken);

        var response = await financialIntelligenceClient.AnalyzeAsync(
            BuildRequest(userId, periodStart, snapshot, options.Value),
            cancellationToken);

        var mappedInsightList = response.Insights
            .Select(item => new MonthHealthInsightResponse
            {
                Type = item.Type,
                Severity = item.Severity,
                Priority = item.Priority,
                Message = item.Message,
                Cause = item.Cause,
                Action = item.Action
            })
            .ToList();

        var mappedReasons = response.Reasons ?? mappedInsightList.Select(i => i.Cause).ToList();
        var mappedActions = response.Actions ?? mappedInsightList.Select(i => i.Action).ToList();
        var mappedPriority = response.Priority ?? mappedInsightList.Max(i => i.Priority);

        return new MonthHealthResponse
        {
            Status = response.Status,
            Score = response.Score,
            Message = response.Message ?? response.Summary.Message,
            Reasons = mappedReasons,
            Actions = mappedActions,
            Priority = mappedPriority,
            Summary = new MonthHealthSummaryResponse
            {
                Message = response.Summary.Message,
                Cause = response.Summary.Cause,
                Action = response.Summary.Action
            },
            Insights = mappedInsightList,
            RecommendedActions = response.RecommendedActions
                .Select(item => new RecommendedActionResponse
                {
                    Id = item.Id,
                    Label = item.Label,
                    Target = item.Target
                })
                .ToList()
        };
    }

    private static FinancialAnalysisRequest BuildRequest(
        Guid userId,
        DateOnly periodStart,
        MonthlyInsightSnapshot snapshot,
        FinancialIntelligenceOptions options)
    {
        return new FinancialAnalysisRequest
        {
            ContractVersion = options.ContractVersion,
            Reference = new FinancialAnalysisReferenceRequest
            {
                UserId = userId.ToString(),
                Month = periodStart.Month,
                Year = periodStart.Year,
                Currency = options.Currency
            },
            Totals = new FinancialAnalysisTotalsRequest
            {
                Income = snapshot.TotalIncome,
                Expense = snapshot.TotalExpense,
                Balance = snapshot.Balance,
                PlannedBudget = snapshot.TotalPlannedBudget,
                BudgetSpent = snapshot.TotalBudgetSpent,
                BudgetRemaining = snapshot.TotalBudgetRemaining,
                FreeToSpend = snapshot.FreeToSpend
            },
            Bills = new FinancialAnalysisBillsRequest
            {
                PendingAmount = snapshot.TotalPendingBills,
                OverdueAmount = snapshot.TotalOverdueBills,
                PendingCount = snapshot.CountPendingBills,
                OverdueCount = snapshot.CountOverdueBills,
                Upcoming7DaysAmount = snapshot.TotalUpcoming7DaysBills,
                Upcoming7DaysCount = snapshot.CountUpcoming7DaysBills
            },
            Categories = snapshot.Categories
                .Select(item => new FinancialAnalysisCategoryRequest
                {
                    CategoryId = item.CategoryId,
                    Name = item.Name,
                    Type = item.Type == TransactionType.Income ? "income" : "expense",
                    Amount = item.Amount
                })
                .ToList()
        };
    }
}
