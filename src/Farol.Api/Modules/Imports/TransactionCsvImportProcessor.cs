using Farol.Domain.Categories;
using Farol.Domain.Ledger;
using Farol.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Farol.Api.Modules.Imports;

public sealed class TransactionCsvImportProcessor(
    FarolDbContext dbContext,
    CsvImportFileReader fileReader,
    CsvImportParser parser)
{
    public async Task<ImportTransactionsCsvResponse> ProcessAsync(
        Guid userId,
        FinancialAccount financialAccount,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var lines = await fileReader.ReadLinesAsync(file, cancellationToken);

        if (lines.Count == 0)
        {
            return BuildEmptyResponse();
        }

        var parseResult = parser.Parse(lines);

        var visibleCategories = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.IsSystem || category.UserId == userId)
            .OrderByDescending(category => category.IsSystem)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken);

        var categoryResolver = new TransactionImportCategoryResolver(visibleCategories);
        var errors = parseResult.Errors.ToList();
        var importedRows = 0;

        foreach (var parsedLine in parseResult.ParsedLines)
        {
            if (!TryResolveCategory(parsedLine.Row, categoryResolver, parsedLine.RowNumber, errors, out var category))
            {
                continue;
            }

            if (!TryQueueTransaction(
                    financialAccount,
                    parsedLine.Row,
                    category,
                    parsedLine.RowNumber,
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
            TotalRows = parseResult.TotalRows,
            ImportedRows = importedRows,
            SkippedRows = parseResult.TotalRows - importedRows,
            Errors = errors
        };
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
