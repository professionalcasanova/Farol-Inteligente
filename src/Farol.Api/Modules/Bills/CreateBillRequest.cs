using System.ComponentModel.DataAnnotations;
using Farol.Domain.Bills;

namespace Farol.Api.Modules.Bills;

public sealed class CreateBillRequest
{
    [Required]
    public string Description { get; init; } = string.Empty;

    [Range(
        typeof(decimal),
        "0.01",
        "999999999999.99",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    public decimal Amount { get; init; }

    [Required]
    public DateOnly DueOn { get; init; }

    public CreateRecurringBillRequest? Recurrence { get; init; }
}

public sealed class CreateRecurringBillRequest
{
    [Required]
    public string Frequency { get; init; } = BillSeries.MonthlyFrequency;

    [Required]
    public string EndMode { get; init; } = BillSeries.OpenEndedEndMode;

    public DateOnly? UntilDate { get; init; }

    [Range(1, int.MaxValue)]
    public int? OccurrenceCount { get; init; }
}
