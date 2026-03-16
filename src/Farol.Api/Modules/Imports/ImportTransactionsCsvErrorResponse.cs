namespace Farol.Api.Modules.Imports;

public sealed class ImportTransactionsCsvErrorResponse
{
    public required int RowNumber { get; init; }
    public required string Message { get; init; }
}
