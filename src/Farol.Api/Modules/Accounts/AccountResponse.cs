using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Accounts;

public sealed class AccountResponse
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required FinancialAccountType Type { get; init; }
    public required bool IsActive { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
}
