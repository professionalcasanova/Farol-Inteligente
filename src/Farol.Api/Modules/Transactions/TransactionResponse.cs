using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Transactions;

public sealed class TransactionResponse
{
    public required Guid Id { get; init; }
    public required Guid FinancialAccountId { get; init; }
    public Guid? CategoryId { get; init; }
    public required TransactionType Type { get; init; }
    public required decimal Amount { get; init; }
    public required string Description { get; init; }
    public required DateOnly OccurredOn { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
