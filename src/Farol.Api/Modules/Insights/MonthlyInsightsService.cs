using Farol.Api.Modules.Bills;
using Farol.Domain.Bills;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Insights;

public sealed class MonthlyInsightsService(
    FarolDbContext dbContext,
    TimeProvider timeProvider,
    BillSeriesExpansionService billSeriesExpansionService)
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
        var currentPeriodStart = new DateOnly(today.Year, today.Month, 1);
        var isFuturePeriod = periodStart > currentPeriodStart;

        await billSeriesExpansionService.ExpandForMonthAsync(
            userId,
            periodStart,
            cancellationToken);

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
                bill.IsPaid,
                bill.BillSeriesId
            })
            .ToListAsync(cancellationToken);
        var seriesIds = bills
            .Where(bill => bill.BillSeriesId.HasValue)
            .Select(bill => bill.BillSeriesId!.Value)
            .Distinct()
            .ToArray();
        var seriesKindsById = seriesIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.BillSeries
                .AsNoTracking()
                .Where(series => seriesIds.Contains(series.Id))
                .ToDictionaryAsync(series => series.Id, series => series.Kind, cancellationToken);

        var pendingBills = isFuturePeriod
            ? bills.Where(bill => !bill.IsPaid).ToList()
            : bills.Where(bill => !bill.IsPaid && bill.DueOn >= today).ToList();

        var upcoming7DaysBills = isFuturePeriod
            ? []
            : bills
                .Where(bill =>
                    !bill.IsPaid &&
                    bill.DueOn >= today &&
                    bill.DueOn <= today.AddDays(7))
                .ToList();

        var overdueBills = isFuturePeriod
            ? []
            : bills.Where(bill => !bill.IsPaid && bill.DueOn < today).ToList();
        var predictableBills = bills
            .Where(bill => !bill.IsPaid && bill.BillSeriesId.HasValue)
            .ToList();
        var recurringBills = predictableBills
            .Where(bill =>
                bill.BillSeriesId.HasValue &&
                seriesKindsById.TryGetValue(bill.BillSeriesId.Value, out var kind) &&
                kind == BillSeries.RecurringKind)
            .ToList();
        var installmentBills = predictableBills
            .Where(bill =>
                bill.BillSeriesId.HasValue &&
                seriesKindsById.TryGetValue(bill.BillSeriesId.Value, out var kind) &&
                kind == BillSeries.InstallmentKind)
            .ToList();
        var maxOverdueDays = overdueBills.Count == 0
            ? 0
            : overdueBills.Max(bill => today.DayNumber - bill.DueOn.DayNumber);

        var totalBudgetRemaining = totalPlannedBudget - totalBudgetSpent;
        var plannedRemaining = Math.Max(totalBudgetRemaining, 0m);
        var unpaidBillsReserve = pendingBills.Sum(bill => bill.Amount) + overdueBills.Sum(bill => bill.Amount);
        var freeToSpend = isFuturePeriod
            ? balance
            : balance - plannedRemaining - unpaidBillsReserve;

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
            isFuturePeriod,
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
            maxOverdueDays,
            predictableBills.Sum(bill => bill.Amount),
            predictableBills.Count,
            recurringBills.Sum(bill => bill.Amount),
            recurringBills.Count,
            installmentBills.Sum(bill => bill.Amount),
            installmentBills.Count,
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
            IsProjection = snapshot.IsFuturePeriod,
            TotalIncome = snapshot.TotalIncome,
            TotalExpense = snapshot.TotalExpense,
            Balance = snapshot.Balance,
            TotalPlannedBudget = snapshot.TotalPlannedBudget,
            TotalBudgetSpent = snapshot.TotalBudgetSpent,
            TotalBudgetRemaining = snapshot.TotalBudgetRemaining,
            PlannedReserve = snapshot.PlannedReserve,
            UnpaidBillsReserve = snapshot.UnpaidBillsReserve,
            PredictableObligationsReserve = snapshot.TotalPredictableObligations,
            PredictableObligationsCount = snapshot.CountPredictableObligations,
            RecurringBillsReserve = snapshot.TotalRecurringObligations,
            RecurringBillsCount = snapshot.CountRecurringObligations,
            InstallmentBillsReserve = snapshot.TotalInstallmentObligations,
            InstallmentBillsCount = snapshot.CountInstallmentObligations,
            FreeToSpend = snapshot.FreeToSpend
        };
    }

    public AlertsResponse BuildAlertsResponse(MonthlyInsightSnapshot snapshot)
    {
        var alerts = new List<AlertResponse>();

        if (snapshot.IsFuturePeriod)
        {
            return new AlertsResponse
            {
                Alerts = alerts
            };
        }

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
                ActionUrl = "/transactions"
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
                ActionUrl = "/bills?status=pending"
            });
        }

        return new AlertsResponse
        {
            Alerts = alerts
        };
    }
}

public sealed record MonthlyInsightSnapshot(
    bool IsFuturePeriod,
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
    int MaxOverdueDays,
    decimal TotalPredictableObligations,
    int CountPredictableObligations,
    decimal TotalRecurringObligations,
    int CountRecurringObligations,
    decimal TotalInstallmentObligations,
    int CountInstallmentObligations,
    IReadOnlyList<MonthlyInsightCategorySnapshot> Categories)
{
    public decimal BudgetOverrun => TotalBudgetSpent - TotalPlannedBudget;
    public decimal PlannedReserve => Math.Max(TotalBudgetRemaining, 0m);
    public decimal UnpaidBillsReserve => TotalPendingBills + TotalOverdueBills;
}

public sealed record MonthlyInsightCategorySnapshot(
    Guid CategoryId,
    string Name,
    TransactionType Type,
    decimal Amount);
