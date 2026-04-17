using System.Text;
using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Imports;

public sealed class TransactionCsvImportProcessor(FarolDbContext dbContext)
{
    public async Task<ImportTransactionsCsvResponse> ProcessAsync(
        Guid userId,
        FinancialAccount financialAccount,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var lines = await ReadLinesAsync(file, cancellationToken);

        if (lines.Count == 0)
        {
            return BuildEmptyResponse();
        }

        if (!TryResolveLayout(lines, out var layout, out var headerLineNumber))
        {
            throw new InvalidOperationException(TransactionCsvParser.InvalidHeaderMessage);
        }

        var visibleCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem || category.UserId == userId)
            .OrderByDescending(category => category.IsSystem)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var categoryResolver = new TransactionImportCategoryResolver(visibleCategories);
        var errors = new List<ImportTransactionsCsvErrorResponse>();
        var importedRows = 0;
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

            if (!TryResolveCategory(parsedRow, categoryResolver, rowNumber, errors, out var category))
            {
                continue;
            }

            if (!TryQueueTransaction(
                    financialAccount,
                    parsedRow,
                    category,
                    rowNumber,
                    errors))
            {
                continue;
            }

            importedRows++;
        }

        if (importedRows > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ImportTransactionsCsvResponse
        {
            TotalRows = totalRows,
            ImportedRows = importedRows,
            SkippedRows = totalRows - importedRows,
            Errors = errors
        };
    }

    private async Task<List<string>> ReadLinesAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(
            file.OpenReadStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);

        var lines = new List<string>();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lines.Add(line);
        }

        return lines;
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

    private static bool TryResolveCategory(
        ParsedTransactionCsvRow parsedRow,
        TransactionImportCategoryResolver categoryResolver,
        int rowNumber,
        ICollection<ImportTransactionsCsvErrorResponse> errors,
        out Category? category)
    {
        category = null;

        if (!string.IsNullOrWhiteSpace(parsedRow.CategoryName))
        {
            if (!categoryResolver.TryResolveExplicit(parsedRow.CategoryName, out category))
            {
                errors.Add(new ImportTransactionsCsvErrorResponse
                {
                    RowNumber = rowNumber,
                    Message = "A categoria informada nao foi encontrada."
                });

                return false;
            }
        }
        else
        {
            category = categoryResolver.ResolveByRule(parsedRow.Description, parsedRow.Type);
        }

        if (category is not null && !category.CanBeAssignedTo(parsedRow.Type))
        {
            errors.Add(new ImportTransactionsCsvErrorResponse
            {
                RowNumber = rowNumber,
                Message = "A categoria informada nao combina com o tipo da transacao."
            });

            return false;
        }

        return true;
    }

    private bool TryQueueTransaction(
        FinancialAccount financialAccount,
        ParsedTransactionCsvRow parsedRow,
        Category? category,
        int rowNumber,
        ICollection<ImportTransactionsCsvErrorResponse> errors)
    {
        try
        {
            dbContext.Transactions.Add(new Transaction(
                financialAccount,
                parsedRow.Type,
                parsedRow.Amount,
                parsedRow.Description,
                parsedRow.OccurredOn,
                category));

            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            ArgumentOutOfRangeException or
            InvalidOperationException)
        {
            errors.Add(new ImportTransactionsCsvErrorResponse
            {
                RowNumber = rowNumber,
                Message = exception.Message
            });

            return false;
        }
    }

    private static ImportTransactionsCsvResponse BuildEmptyResponse()
    {
        return new ImportTransactionsCsvResponse
        {
            TotalRows = 0,
            ImportedRows = 0,
            SkippedRows = 0,
            Errors = Array.Empty<ImportTransactionsCsvErrorResponse>()
        };
    }
}
