namespace Farol.Api.Modules.Transactions;

public sealed class TransactionHistoryResponse
{
    public required IReadOnlyList<TransactionResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalItems { get; init; }

    public required int TotalPages { get; init; }
}
