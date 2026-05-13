using System.Text;
using Microsoft.AspNetCore.Http;

namespace Farol.Api.Modules.Imports;

internal sealed record CsvImportParsedLine(
    int RowNumber,
    ParsedTransactionCsvRow Row);

internal sealed record CsvImportParseResult(
    int TotalRows,
    IReadOnlyList<CsvImportParsedLine> ParsedLines,
    IReadOnlyList<ImportTransactionsCsvErrorResponse> Errors);

public sealed class CsvImportFileReader
{
    private const int MaxCsvLines = 5000;
    private const int MaxCsvLineCharacters = 10000;

    internal async Task<IReadOnlyList<string>> ReadLinesAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var lines = new List<string>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;

            if (lineNumber > MaxCsvLines)
            {
                throw new InvalidOperationException("O CSV excede o limite maximo de 5000 linhas.");
            }

            if (line.Length > MaxCsvLineCharacters)
            {
                throw new InvalidOperationException(
                    $"A linha {lineNumber} do CSV excede o tamanho maximo permitido.");
            }

            lines.Add(line);
        }

        return lines;
    }
}

public sealed class CsvImportParser
{
    internal CsvImportParseResult Parse(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return new CsvImportParseResult(
                0,
                [],
                []);
        }

        if (!TryResolveLayout(lines, out var layout, out var headerLineNumber))
        {
            throw new InvalidOperationException(TransactionCsvParser.InvalidHeaderMessage);
        }

        var parsedLines = new List<CsvImportParsedLine>();
        var errors = new List<ImportTransactionsCsvErrorResponse>();
        var totalRows = 0;

        for (var index = headerLineNumber; index < lines.Count; index++)
        {
            var rowNumber = index + 1;
            var line = lines[index];

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            totalRows++;

            if (!TransactionCsvParser.TryParseRow(line, layout, rowNumber, out var parsedRow, out var error))
            {
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = error
                });
                continue;
            }

            parsedLines.Add(new CsvImportParsedLine(rowNumber, parsedRow));
        }

        return new CsvImportParseResult(totalRows, parsedLines, errors);
    }

    private static bool TryResolveLayout(
        IReadOnlyList<string> lines,
        out TransactionCsvLayout layout,
        out int headerLineNumber)
    {
        layout = null!;
        headerLineNumber = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            var candidate = lines[index];

            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!TransactionCsvParser.TryParseHeader(candidate, out layout, out _))
            {
                continue;
            }

            headerLineNumber = index + 1;
            return true;
        }

        return false;
    }
}
