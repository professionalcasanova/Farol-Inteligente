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

        if (snapshot.IsFuturePeriod)
        {
            return BuildProjectedMonthHealthResponse(snapshot);
        }

        FinancialAnalysisResponse response;

        try
        {
            response = await financialIntelligenceClient.AnalyzeAsync(
                BuildRequest(userId, periodStart, snapshot, options.Value),
                cancellationToken);
        }
        catch (FinancialIntelligenceUnavailableException)
        {
            return BuildLocalFallbackMonthHealthResponse(snapshot);
        }

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

    private static MonthHealthResponse BuildProjectedMonthHealthResponse(MonthlyInsightSnapshot snapshot)
    {
        var hasMappedCommitments = snapshot.TotalPendingBills > 0 || snapshot.TotalPlannedBudget > 0;
        var needsIncomeConfirmation = hasMappedCommitments && snapshot.TotalIncome <= 0;
        var status = needsIncomeConfirmation ? "attention" : "healthy";
        var message = needsIncomeConfirmation
            ? "O proximo mes ja tem compromissos mapeados, mas ainda esta em projecao."
            : "Este mes futuro esta sendo tratado como projecao, nao como divida ja realizada.";
        var cause = hasMappedCommitments
            ? "Reservas do planejamento e contas previstas aparecem aqui como preparacao do mes. Elas ainda nao contam como saida realizada."
            : "Ainda nao ha movimentacoes nem compromissos suficientes para uma leitura de risco. Este painel serve como preparo do proximo ciclo.";
        var action = needsIncomeConfirmation
            ? "Confirme as entradas esperadas e ajuste planejamento ou vencimentos antes do mes comecar."
            : "Use este periodo para organizar entradas, reservas e vencimentos sem tratar previsoes como atraso.";

        return new MonthHealthResponse
        {
            Status = status,
            Score = needsIncomeConfirmation ? 78 : 92,
            Message = message,
            Reasons = [cause],
            Actions = [action],
            Priority = needsIncomeConfirmation ? AttentionPriority : HealthyPriority,
            Summary = new MonthHealthSummaryResponse
            {
                Message = message,
                Cause = cause,
                Action = action
            },
            Insights = [],
            RecommendedActions = BuildProjectedRecommendedActions(needsIncomeConfirmation, snapshot)
        };
    }

    private static MonthHealthResponse BuildLocalFallbackMonthHealthResponse(MonthlyInsightSnapshot snapshot)
    {
        var isCritical = snapshot.TotalOverdueBills > 0 || snapshot.Balance < 0 || snapshot.FreeToSpend < 0;
        var status = isCritical ? "critical" : "attention";
        var score = isCritical ? 45 : 72;
        var priority = isCritical ? CriticalPriority : AttentionPriority;
        var message = isCritical
            ? "A inteligencia financeira esta indisponivel; exibindo uma leitura local com sinais de atencao alta."
            : "A inteligencia financeira esta indisponivel; exibindo uma leitura local temporaria.";
        var cause = isCritical
            ? "A leitura local encontrou saldo, dinheiro livre ou contas vencidas em zona de risco."
            : "A leitura local usa apenas saldos, orcamento e contas ja registrados, sem analise avancada.";
        var action = isCritical
            ? "Revise contas vencidas, saldo disponivel e gastos do mes antes de assumir novos compromissos."
            : "Use esta leitura local como referencia temporaria e confira novamente em alguns instantes.";

        return new MonthHealthResponse
        {
            Status = status,
            Score = score,
            Message = message,
            Reasons = [cause],
            Actions = [action],
            Priority = priority,
            Summary = new MonthHealthSummaryResponse
            {
                Message = message,
                Cause = cause,
                Action = action
            },
            Insights = [],
            RecommendedActions = BuildLocalFallbackRecommendedActions(snapshot, isCritical)
        };
    }

    private static IReadOnlyList<RecommendedActionResponse> BuildLocalFallbackRecommendedActions(
        MonthlyInsightSnapshot snapshot,
        bool isCritical)
    {
        var actions = new List<RecommendedActionResponse>();

        if (snapshot.TotalOverdueBills > 0)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "review_overdue_bills",
                Label = "Resolver contas vencidas",
                Target = "/bills?status=overdue"
            });
        }

        if (isCritical && snapshot.Balance < 0)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "review_month_transactions",
                Label = "Revisar saidas do mes",
                Target = "/transactions"
            });
        }

        if (actions.Count == 0)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "retry_month_health",
                Label = "Atualizar saude do mes",
                Target = "/dashboard"
            });
        }

        return actions;
    }

    private static IReadOnlyList<RecommendedActionResponse> BuildProjectedRecommendedActions(
        bool needsIncomeConfirmation,
        MonthlyInsightSnapshot snapshot)
    {
        var actions = new List<RecommendedActionResponse>();

        if (needsIncomeConfirmation)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "review_planned_income",
                Label = "Registrar entradas previstas",
                Target = "/transactions"
            });
        }

        if (snapshot.TotalPlannedBudget > 0)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "review_budget_projection",
                Label = "Revisar planejamento",
                Target = "/budget"
            });
        }

        if (snapshot.TotalPendingBills > 0)
        {
            actions.Add(new RecommendedActionResponse
            {
                Id = "review_next_bills",
                Label = "Conferir vencimentos",
                Target = "/bills?status=pending"
            });
        }

        return actions;
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
