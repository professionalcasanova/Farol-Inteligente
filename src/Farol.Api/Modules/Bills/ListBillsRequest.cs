using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Bills;

public sealed class ListBillsRequest
{
    [Range(1, 12)]
    public int? Month { get; init; }

    [Range(2000, 2100)]
    public int? Year { get; init; }

    public string? Status { get; init; }
}
