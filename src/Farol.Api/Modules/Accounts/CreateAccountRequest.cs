using System.ComponentModel.DataAnnotations;
using Farol.Domain.Ledger;

namespace Farol.Api.Modules.Accounts;

public sealed class CreateAccountRequest
{
    [Required]
    public string Name { get; init; } = string.Empty;

    [Required]
    public FinancialAccountType Type { get; init; }
}
