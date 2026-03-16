namespace Farol.Api.Modules.Imports;

public sealed class ImportTransactionsCsvResponse
{
    public required int TotalRows { get; init; }
    public required int ImportedRows { get; init; }
    public required int SkippedRows { get; init; }
    public required IReadOnlyList<ImportTransactionsCsvErrorResponse> Errors { get; init; }
}
