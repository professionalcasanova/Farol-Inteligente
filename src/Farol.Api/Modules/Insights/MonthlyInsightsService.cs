using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Insights;

public sealed class MonthlyInsightsService(FarolDbContext dbContext, TimeProvider timeProvider)
{
    private const string HighSeverity = "high";
    private const string MediumSeverity = "medium";

    public async Task<MonthlyInsightSnapshot> GetMonthlySnapshotAsync(
        Guid userId,
        DateOnly periodStart,
        CancellationToken cancellationToken)
    {
        var periodEnd = periodStart.AddMonths(1);
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

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
        var plannedRemaining = Math.Max(totalBudgetRemaining, 0m);
        var freeToSpend = balance - plannedRemaining;

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

        var upcoming7DaysBills = bills
            .Where(bill =>
                !bill.IsPaid &&
                bill.DueOn >= today &&
                bill.DueOn <= today.AddDays(7))
            .ToList();

        var overdueBills = bills
            .Where(bill => !bill.IsPaid && bill.DueOn < today)
            .ToList();

        var categoryIdsForSummary = transactions
            .Where(transaction => transaction.CategoryId.HasValue)
            .Select(transaction => transaction.CategoryId!.Value)
            .Distinct()
            .ToArray();

        var categoryNames = categoryIdsForSummary.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Categories
                .AsNoTracking()
                .Where(category => categoryIdsForSummary.Contains(category.Id))
                .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);

        var categories = transactions
            .Where(transaction =>
                transaction.CategoryId.HasValue &&
                categoryNames.ContainsKey(transaction.CategoryId.Value))
            .GroupBy(transaction => new { CategoryId = transaction.CategoryId!.Value, transaction.Type })
            .Select(group => new MonthlyInsightCategorySnapshot(
                group.Key.CategoryId,
                categoryNames[group.Key.CategoryId],
                group.Key.Type,
                group.Sum(item => item.Amount)))
            .ToList();

        return new MonthlyInsightSnapshot(
            totalIncome,
            totalExpense,
            balance,
            totalPlannedBudget,
            totalBudgetSpent,
            totalBudgetRemaining,
            freeToSpend,
            pendingBills.Sum(bill => bill.Amount),
            overdueBills.Sum(bill => bill.Amount),
            pendingBills.Count,
            overdueBills.Count,
            upcoming7DaysBills.Sum(bill => bill.Amount),
            upcoming7DaysBills.Count,
            categories);
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
    int CountOverdueBills,
    decimal TotalUpcoming7DaysBills,
    int CountUpcoming7DaysBills,
    IReadOnlyList<MonthlyInsightCategorySnapshot> Categories)
{
    public decimal BudgetOverrun => TotalBudgetSpent - TotalPlannedBudget;
}

public sealed record MonthlyInsightCategorySnapshot(
    Guid CategoryId,
    string Name,
    TransactionType Type,
    decimal Amount);
