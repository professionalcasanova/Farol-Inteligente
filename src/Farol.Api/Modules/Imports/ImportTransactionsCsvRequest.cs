using Microsoft.AspNetCore.Http;

namespace Farol.Api.Modules.Imports;

public sealed class ImportTransactionsCsvRequest
{
    public Guid FinancialAccountId { get; init; }
    public IFormFile? File { get; init; }
}
