using Farol.Domain.Bills;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Bills;

public sealed class BillPaymentService(
    FarolDbContext dbContext,
    TimeProvider timeProvider)
{
    private const string PaymentDescriptionPrefix = "Pagamento da conta: ";
    private const int TransactionDescriptionMaxLength = 255;

    public async Task PayAsync(
        Bill bill,
        FinancialAccount account,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bill);
        ArgumentNullException.ThrowIfNull(account);

        if (bill.UserId != account.UserId)
        {
            throw new InvalidOperationException("Financial account must belong to the same user as the bill.");
        }

        if (bill.IsPaid)
        {
            return;
        }

        var paidAtUtc = timeProvider.GetUtcNow();
        var transaction = new Transaction(
            account,
            TransactionType.Expense,
            bill.Amount,
            BuildPaymentDescription(bill.Description),
            DateOnly.FromDateTime(paidAtUtc.UtcDateTime));

        dbContext.Transactions.Add(transaction);
        bill.MarkAsPaid(paidAtUtc, transaction.Id);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnpayAsync(Bill bill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bill);

        if (!bill.IsPaid)
        {
            return;
        }

        if (bill.PaidTransactionId.HasValue)
        {
            var paymentTransaction = await dbContext.Transactions
                .SingleOrDefaultAsync(
                    transaction =>
                        transaction.Id == bill.PaidTransactionId.Value &&
                        transaction.UserId == bill.UserId,
                    cancellationToken);

            if (paymentTransaction is not null)
            {
                dbContext.Transactions.Remove(paymentTransaction);
            }
        }

        bill.MarkAsUnpaid();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildPaymentDescription(string billDescription)
    {
        var description = $"{PaymentDescriptionPrefix}{billDescription}".Trim();

        if (description.Length <= TransactionDescriptionMaxLength)
        {
            return description;
        }

        return description[..TransactionDescriptionMaxLength];
    }
}
