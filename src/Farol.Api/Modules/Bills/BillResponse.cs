namespace Farol.Api.Modules.Bills;

public sealed class BillResponse
{
    public required Guid Id { get; init; }
    public required string Description { get; init; }
    public required decimal Amount { get; init; }
    public required DateOnly DueOn { get; init; }
    public required bool IsPaid { get; init; }
    public required DateTimeOffset? PaidAtUtc { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required string Status { get; init; }
}
