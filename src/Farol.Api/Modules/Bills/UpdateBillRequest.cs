using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Bills;

public sealed class UpdateBillRequest
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

    public string? Scope { get; init; }
}
