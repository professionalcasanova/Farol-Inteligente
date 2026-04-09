using Farol.Domain.Ledger;
using Microsoft.Extensions.Options;

namespace Farol.Api.Modules.Insights;

public sealed class FinancialIntelligenceService(
    MonthlyInsightsService monthlyInsightsService,
    IFinancialIntelligenceClient financialIntelligenceClient,
    IOptions<FinancialIntelligenceOptions> options)
{
    private const int HealthyPriority = 10;
    private const int AttentionPriority = 60;
    private const int CriticalPriority = 100;

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

        var mappedReasons = MapFallbackList(
            response.Reasons,
            mappedInsightList.Select(i => i.Cause),
            response.Summary.Cause);
        var mappedActions = MapFallbackList(
            response.Actions,
            mappedInsightList.Select(i => i.Action),
            response.Summary.Action);
        var mappedPriority = response.Priority
            ?? mappedInsightList.MaxBy(static insight => insight.Priority)?.Priority
            ?? ResolvePriorityFromStatus(response.Status);

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

    private static IReadOnlyList<string> MapFallbackList(
        IReadOnlyList<string>? values,
        IEnumerable<string> insightValues,
        string summaryValue)
    {
        if (values is { Count: > 0 })
        {
            return values;
        }

        var mappedInsightValues = insightValues
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct()
            .ToList();

        if (mappedInsightValues.Count > 0)
        {
            return mappedInsightValues;
        }

        return string.IsNullOrWhiteSpace(summaryValue)
            ? Array.Empty<string>()
            : [summaryValue];
    }

    private static int ResolvePriorityFromStatus(string status)
    {
        return status switch
        {
            "critical" => CriticalPriority,
            "attention" => AttentionPriority,
            _ => HealthyPriority
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
                Upcoming7DaysCount = snapshot.CountUpcoming7DaysBills,
                MaxOverdueDays = snapshot.MaxOverdueDays,
                PredictableAmount = snapshot.TotalPredictableObligations,
                PredictableCount = snapshot.CountPredictableObligations,
                RecurringAmount = snapshot.TotalRecurringObligations,
                RecurringCount = snapshot.CountRecurringObligations,
                InstallmentAmount = snapshot.TotalInstallmentObligations,
                InstallmentCount = snapshot.CountInstallmentObligations
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
