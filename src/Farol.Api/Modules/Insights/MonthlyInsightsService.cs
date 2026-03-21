using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Insights;

public sealed class MonthlyInsightsService(FarolDbContext dbContext)
{
    private const string HealthyStatus = "healthy";
    private const string AttentionStatus = "attention";
    private const string CriticalStatus = "critical";

    private const string HighSeverity = "high";
    private const string MediumSeverity = "medium";

    private const string OverdueBillsType = "overdue_bills";
    private const string NegativeFreeMoneyType = "negative_free_money";
    private const string BudgetOverspentType = "budget_overspent";
    private const string PendingBillsPressureType = "pending_bills_pressure";

    public async Task<MonthlyInsightSnapshot> GetMonthlySnapshotAsync(
        Guid userId,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        var periodEnd = periodStart.AddMonths(1);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var transactions = await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.UserId == userId &&
                transaction.OccurredOn >= periodStart &&
                transaction.OccurredOn < periodEnd)
            .Select(transaction => new
            {
                transaction.Type,
                transaction.Amount,
                transaction.CategoryId
            })
            .ToListAsync(cancellationToken);

        var totalIncome = transactions
            .Where(transaction => transaction.Type == TransactionType.Income)
            .Sum(transaction => transaction.Amount);

        var totalExpense = transactions
            .Where(transaction => transaction.Type == TransactionType.Expense)
            .Sum(transaction => transaction.Amount);

        var balance = totalIncome - totalExpense;

        var budget = await dbContext.MonthlyBudgets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.UserId == userId &&
                    item.Month == periodStart.Month &&
                    item.Year == periodStart.Year,
                cancellationToken);

        var totalPlannedBudget = 0m;
        var totalBudgetSpent = 0m;

        if (budget is not null)
        {
            var budgetItems = await dbContext.MonthlyBudgetCategories
                .AsNoTracking()
                .Where(item => item.MonthlyBudgetId == budget.Id)
                .Select(item => new
                {
                    item.CategoryId,
                    item.PlannedAmount
                })
                .ToListAsync(cancellationToken);

            var categoryIds = budgetItems
                .Select(item => item.CategoryId)
                .ToArray();

            var spentByCategory = categoryIds.Length == 0
                ? new Dictionary<Guid, decimal>()
                : transactions
                    .Where(transaction =>
                        transaction.Type == TransactionType.Expense &&
                        transaction.CategoryId.HasValue &&
                        categoryIds.Contains(transaction.CategoryId.Value))
                    .GroupBy(transaction => transaction.CategoryId!.Value)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Sum(transaction => transaction.Amount));

            totalPlannedBudget = budgetItems.Sum(item => item.PlannedAmount);
            totalBudgetSpent = budgetItems.Sum(item =>
            {
                spentByCategory.TryGetValue(item.CategoryId, out var spent);
                return spent;
            });
        }

        var totalBudgetRemaining = totalPlannedBudget - totalBudgetSpent;
        var reservedBudget = totalBudgetRemaining > 0 ? totalBudgetRemaining : 0;

        var bills = await dbContext.Bills
            .AsNoTracking()
            .Where(bill =>
                bill.UserId == userId &&
                bill.DueOn >= periodStart &&
                bill.DueOn < periodEnd)
            .Select(bill => new
            {
                bill.Amount,
                bill.DueOn,
                bill.IsPaid
            })
            .ToListAsync(cancellationToken);

        var pendingBills = bills
            .Where(bill => !bill.IsPaid && bill.DueOn >= today)
            .ToList();

        var overdueBills = bills
            .Where(bill => !bill.IsPaid && bill.DueOn < today)
            .ToList();

        return new MonthlyInsightSnapshot(
            totalIncome,
            totalExpense,
            balance,
            totalPlannedBudget,
            totalBudgetSpent,
            totalBudgetRemaining,
            balance - reservedBudget,
            pendingBills.Sum(bill => bill.Amount),
            overdueBills.Sum(bill => bill.Amount),
            pendingBills.Count,
            overdueBills.Count);
    }

    public FreeMoneyResponse BuildFreeMoneyResponse(
        MonthlyInsightSnapshot snapshot,
        int month,
        int year)
    {
        return new FreeMoneyResponse
        {
            Month = month,
            Year = year,
            TotalIncome = snapshot.TotalIncome,
            TotalExpense = snapshot.TotalExpense,
            Balance = snapshot.Balance,
            TotalPlannedBudget = snapshot.TotalPlannedBudget,
            TotalBudgetSpent = snapshot.TotalBudgetSpent,
            TotalBudgetRemaining = snapshot.TotalBudgetRemaining,
            FreeToSpend = snapshot.FreeToSpend
        };
    }

    public AlertsResponse BuildAlertsResponse(MonthlyInsightSnapshot snapshot)
    {
        var alerts = new List<AlertResponse>();

        if (snapshot.TotalOverdueBills > 0)
        {
            alerts.Add(new AlertResponse
            {
                Type = "overdue_bills",
                Severity = HighSeverity,
                Message = "Voce tem contas vencidas que precisam de atencao imediata.",
                Amount = snapshot.TotalOverdueBills,
                ActionUrl = "/bills?status=overdue"
            });
        }

        if (snapshot.FreeToSpend < snapshot.TotalIncome * 0.2m)
        {
            alerts.Add(new AlertResponse
            {
                Type = "low_balance",
                Severity = MediumSeverity,
                Message = "Seu dinheiro livre para o mes esta baixo.",
                Amount = snapshot.FreeToSpend,
                ActionUrl = "/dashboard"
            });
        }

        if (snapshot.TotalPlannedBudget > 0 && snapshot.TotalBudgetSpent > snapshot.TotalPlannedBudget)
        {
            alerts.Add(new AlertResponse
            {
                Type = "budget_overspent",
                Severity = HighSeverity,
                Message = "Seu orcamento do mes ja foi estourado.",
                Amount = snapshot.TotalBudgetSpent - snapshot.TotalPlannedBudget,
                ActionUrl = "/budget"
            });
        }

        if (snapshot.TotalPendingBills > snapshot.TotalIncome * 0.5m)
        {
            alerts.Add(new AlertResponse
            {
                Type = "many_pending_bills",
                Severity = MediumSeverity,
                Message = "Voce ainda tem muitas contas para pagar neste mes.",
                Amount = snapshot.TotalPendingBills,
                ActionUrl = "/bills"
            });
        }

        return new AlertsResponse
        {
            Alerts = alerts
        };
    }

    public MonthHealthResponse BuildMonthHealthResponse(MonthlyInsightSnapshot snapshot)
    {
        var insights = GetActiveInsights(snapshot)
            .OrderByDescending(item => item.Priority)
            .Take(3)
            .ToList();

        var status = ResolveStatus(snapshot);
        var summary = insights.Count > 0
            ? new MonthHealthSummaryResponse
            {
                Message = insights[0].Message,
                Cause = insights[0].Cause,
                Action = insights[0].Action
            }
            : new MonthHealthSummaryResponse
            {
                Message = "Seu mes esta sob controle ate aqui.",
                Cause = "Voce mantem folga no mes e sem sinais fortes de pressao imediata.",
                Action = "Continue acompanhando orcamento e vencimentos para manter a margem."
            };

        return new MonthHealthResponse
        {
            Status = status,
            Summary = summary,
            Insights = insights
        };
    }

    private static string ResolveStatus(MonthlyInsightSnapshot snapshot)
    {
        if (snapshot.CountOverdueBills > 0)
        {
            return CriticalStatus;
        }

        if (snapshot.FreeToSpend < 0)
        {
            return CriticalStatus;
        }

        if (snapshot.TotalPlannedBudget > 0 && snapshot.BudgetOverrun >= 50m)
        {
            return AttentionStatus;
        }

        if (snapshot.FreeToSpend >= 0 && snapshot.CountPendingBills >= 2 && snapshot.TotalPendingBills > snapshot.FreeToSpend)
        {
            return AttentionStatus;
        }

        return HealthyStatus;
    }

    private static List<MonthHealthInsightResponse> GetActiveInsights(MonthlyInsightSnapshot snapshot)
    {
        var insights = new List<MonthHealthInsightResponse>();

        if (snapshot.CountOverdueBills > 0)
        {
            insights.Add(new MonthHealthInsightResponse
            {
                Type = OverdueBillsType,
                Severity = HighSeverity,
                Priority = 100,
                Message = "Voce tem contas vencidas que precisam de atencao imediata.",
                Cause = "Ha vencimentos atrasados pressionando seu mes.",
                Action = "Priorize quitar ou renegociar as contas vencidas hoje."
            });
        }

        if (snapshot.FreeToSpend < 0)
        {
            insights.Add(new MonthHealthInsightResponse
            {
                Type = NegativeFreeMoneyType,
                Severity = HighSeverity,
                Priority = 90,
                Message = "Voce esta no vermelho neste mes.",
                Cause = "Depois das despesas e compromissos, seu dinheiro livre ficou negativo.",
                Action = "Pause novos gastos e revise as maiores saidas do mes."
            });
        }

        if (snapshot.TotalPlannedBudget > 0 && snapshot.BudgetOverrun >= 50m)
        {
            insights.Add(new MonthHealthInsightResponse
            {
                Type = BudgetOverspentType,
                Severity = MediumSeverity,
                Priority = 70,
                Message = "Seu orcamento do mes ja saiu do plano.",
                Cause = "Voce gastou mais do que planejou nas categorias acompanhadas.",
                Action = "Reduza gastos ajustaveis e reavalie o restante do mes."
            });
        }

        if (snapshot.FreeToSpend >= 0 && snapshot.CountPendingBills >= 2 && snapshot.TotalPendingBills > snapshot.FreeToSpend)
        {
            insights.Add(new MonthHealthInsightResponse
            {
                Type = PendingBillsPressureType,
                Severity = MediumSeverity,
                Priority = 60,
                Message = "As contas ainda pendentes ja consomem sua folga do mes.",
                Cause = "Os proximos vencimentos estao ocupando todo o dinheiro livre disponivel.",
                Action = "Organize a ordem de pagamento e preserve caixa para o essencial."
            });
        }

        return insights;
    }
}

public sealed record MonthlyInsightSnapshot(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    decimal TotalPlannedBudget,
    decimal TotalBudgetSpent,
    decimal TotalBudgetRemaining,
    decimal FreeToSpend,
    decimal TotalPendingBills,
    decimal TotalOverdueBills,
    int CountPendingBills,
    int CountOverdueBills)
{
    public decimal BudgetOverrun => TotalBudgetSpent - TotalPlannedBudget;
}
