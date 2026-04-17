using System.ComponentModel.DataAnnotations;

namespace Farol.Api.Modules.Bills;

public sealed class PayBillRequest
{
    [Required]
    public Guid FinancialAccountId { get; init; }
}
