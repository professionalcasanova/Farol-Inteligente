using System.ComponentModel.DataAnnotations;
using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Transactions;

public sealed class UpdateTransactionRequest
{
    [Required]
    public Guid FinancialAccountId { get; init; }

    public Guid? CategoryId { get; init; }

    [Required]
    public TransactionType Type { get; init; }

    [Range(
        typeof(decimal),
        "0.01",
        "999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal Amount { get; init; }

    [Required]
    public string Description { get; init; } = string.Empty;

    [Required]
    public DateOnly OccurredOn { get; init; }
}
