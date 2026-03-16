using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Dashboard;

public sealed class MonthlySummaryCategoryResponse
{
    public Guid? CategoryId { get; init; }
    public required string CategoryName { get; init; }
    public required TransactionType Type { get; init; }
    public required decimal Total { get; init; }
}
